param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\Doutput\cpu-protected-forensic.bin'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\Doutput\cpu-protected-forensic-decoded.txt'),
    # A targeted stream is bounded at 65,536 instructions.  Retain that entire
    # bound by default: the initiating #NP and its short loader classifier occur
    # near the beginning, and dropping the first 15,536 records obscured the
    # cause while leaving only the much later cleanup path visible.
    [int]$TailInstructions = 65536
)

$resolvedInput = [IO.Path]::GetFullPath($InputPath)
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$stream = [IO.File]::Open($resolvedInput, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
$reader = [IO.BinaryReader]::new($stream, [Text.Encoding]::UTF8, $false)
$tail = [Collections.Generic.Queue[string]]::new()
$writeTail = [Collections.Generic.Queue[string]]::new()
$events = [Collections.Generic.List[string]]::new()

try {
    $magic = $reader.ReadUInt32()
    if ($magic -ne 0x56434654) { throw 'Not a Cromwell CPU forensic stream.' }
    $version = $reader.ReadUInt32()
    $startedTicks = $reader.ReadInt64()
    $description = $reader.ReadString()
    $terminal = '(stream did not contain a terminal record)'
    $instructionCount = [UInt64]0

    while ($stream.Position -lt $stream.Length) {
        $recordType = $reader.ReadByte()
        $sequence = $reader.ReadUInt64()
        switch ($recordType) {
            1 {
                $cs = $reader.ReadUInt16(); $ip = $reader.ReadUInt16()
                $ss = $reader.ReadUInt16(); $sp = $reader.ReadUInt16()
                $flags = $reader.ReadUInt16(); $msw = $reader.ReadUInt16()
                $ax = $reader.ReadUInt16(); $bx = $reader.ReadUInt16()
                $cx = $reader.ReadUInt16(); $dx = $reader.ReadUInt16()
                $si = $reader.ReadUInt16(); $di = $reader.ReadUInt16(); $bp = $reader.ReadUInt16()
                $ds = $reader.ReadUInt16(); $es = $reader.ReadUInt16()
                $cpl = $reader.ReadByte(); $pm = $reader.ReadByte()
                $opcode = $reader.ReadByte(); $rep = $reader.ReadByte(); $segmentOverride = $reader.ReadSByte()
                $csBase = $reader.ReadUInt32(); $csLimit = $reader.ReadUInt16()
                $line = ('#{0:D12} {1:X4}:{2:X4} OP={3:X2} AX={4:X4} BX={5:X4} CX={6:X4} DX={7:X4} SI={8:X4} DI={9:X4} BP={10:X4} DS={11:X4} ES={12:X4} SS:SP={13:X4}:{14:X4} FL={15:X4} MSW={16:X4} CPL={17} PM={18} REP={19:X2} SEG={20} CSBASE={21:X6} CSLIM={22:X4}' -f
                    $sequence,$cs,$ip,$opcode,$ax,$bx,$cx,$dx,$si,$di,$bp,$ds,$es,$ss,$sp,$flags,$msw,$cpl,$pm,$rep,$segmentOverride,$csBase,$csLimit)
                $tail.Enqueue($line)
                while ($tail.Count -gt $TailInstructions) { [void]$tail.Dequeue() }
                $instructionCount = $sequence + 1
            }
            2 { $events.Add(('#{0:D12} EVENT {1}' -f $sequence, $reader.ReadString())) }
            3 { $terminal = $reader.ReadString(); break }
            4 {
                $address = $reader.ReadUInt32(); $size = $reader.ReadByte(); $value = $reader.ReadUInt16()
                $writeTail.Enqueue(('#{0:D12} MEMW{1} [{2:X6}]={3:X4}' -f $sequence,($size * 8),$address,$value))
                while ($writeTail.Count -gt $TailInstructions) { [void]$writeTail.Dequeue() }
            }
            default { throw "Unknown forensic record type $recordType at file offset $($stream.Position - 9)." }
        }
    }

    $output = [Text.StringBuilder]::new()
    [void]$output.AppendLine($description)
    [void]$output.AppendLine("Format version: $version")
    [void]$output.AppendLine("Started UTC: $([DateTime]::new($startedTicks, [DateTimeKind]::Utc).ToString('O'))")
    [void]$output.AppendLine("Instructions recorded: $instructionCount")
    [void]$output.AppendLine("Terminal reason: $terminal")
    [void]$output.AppendLine()
    [void]$output.AppendLine('--- forensic events ---')
    foreach ($line in $events) { [void]$output.AppendLine($line) }
    [void]$output.AppendLine()
    [void]$output.AppendLine("--- last $($tail.Count) instructions ---")
    foreach ($line in $tail) { [void]$output.AppendLine($line) }
    [void]$output.AppendLine()
    [void]$output.AppendLine("--- last $($writeTail.Count) memory writes ---")
    foreach ($line in $writeTail) { [void]$output.AppendLine($line) }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
    [IO.File]::WriteAllText($resolvedOutput, $output.ToString(), [Text.UTF8Encoding]::new($false))
    $resolvedOutput
}
finally {
    $reader.Dispose()
}
