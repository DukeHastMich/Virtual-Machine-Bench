param(
    [Parameter(Mandatory=$true)][string]$Image,
    [string]$Find = "SETUP.EXE",
    [string]$Extract
)

$source = @'
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class FatInspector {
  static ushort U16(byte[] b,int o) { return (ushort)(b[o]|b[o+1]<<8); }
  static uint U32(byte[] b,int o) { return (uint)(b[o]|b[o+1]<<8|b[o+2]<<16|b[o+3]<<24); }
  static void ReadAt(FileStream f,long o,byte[] b) { f.Position=o; int n=0,r; while(n<b.Length&&(r=f.Read(b,n,b.Length-n))>0)n+=r; if(n!=b.Length)throw new EndOfStreamException(); }
  public static string Run(string path,string find,string extract) {
    using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
      byte[] boot=new byte[512]; ReadAt(f,0,boot); long vol=0;
      if(boot[510]==0x55&&boot[511]==0xAA && !(boot[0]==0xEB||boot[0]==0xE9)) { uint lba=U32(boot,446+8); vol=(long)lba*512; ReadAt(f,vol,boot); }
      int bps=U16(boot,11),spc=boot[13],res=U16(boot,14),nf=boot[16],roots=U16(boot,17); uint total=U16(boot,19); if(total==0)total=U32(boot,32); uint spf=U16(boot,22);
      long rootSecs=((long)roots*32+bps-1)/bps, firstData=res+(long)nf*spf+rootSecs, clusters=(total-firstData)/spc; bool fat12=clusters<4085;
      byte[] fat=new byte[spf*bps]; ReadAt(f,vol+(long)res*bps,fat);
      long rootOff=vol+(res+(long)nf*spf)*bps;
      var hits=new List<string>(); Walk(f,vol,bps,spc,firstData,fat,fat12,rootOff,(int)(rootSecs*bps),"",find,extract,hits,new HashSet<int>());
      return "volumeOffset="+vol+" bps="+bps+" spc="+spc+" FAT"+(fat12?"12":"16")+" clusters="+clusters+Environment.NewLine+string.Join(Environment.NewLine,hits);
    }
  }
  static int Next(byte[] fat,int c,bool f12) { int o=f12?c+c/2:c*2; if(c<2||o<0||o+1>=fat.Length)return -1; int v=U16(fat,o); return f12?((c&1)==0?v&0xFFF:v>>4):v; }
  static long ClOff(long vol,int bps,int spc,long first,int c) { return vol+(first+(long)(c-2)*spc)*bps; }
  static byte[] Chain(FileStream f,long vol,int bps,int spc,long first,byte[] fat,bool f12,int start,int size) {
    byte[] all=new byte[size]; int p=0,c=start; var seen=new HashSet<int>(); byte[] blk=new byte[bps*spc];
    while(p<size&&c>=2&&seen.Add(c)){long co=ClOff(vol,bps,spc,first,c);if(co<vol||co+blk.Length>f.Length)break;ReadAt(f,co,blk);int n=Math.Min(blk.Length,size-p);Buffer.BlockCopy(blk,0,all,p,n);p+=n;c=Next(fat,c,f12);if(c<2||c>=(f12?0xFF8:0xFFF8))break;} if(p==all.Length)return all;Array.Resize(ref all,p);return all;
  }
  static void Walk(FileStream f,long vol,int bps,int spc,long first,byte[] fat,bool f12,long off,int len,string prefix,string find,string extract,List<string> hits,HashSet<int> dirs) {
    byte[] d=new byte[len]; ReadAt(f,off,d); for(int o=0;o+32<=d.Length;o+=32){if(d[o]==0)break;if(d[o]==0xE5||d[o+11]==0x0F||(d[o+11]&8)!=0)continue;
      string n=Encoding.ASCII.GetString(d,o,8).TrimEnd()+((Encoding.ASCII.GetString(d,o+8,3).TrimEnd().Length>0)?"."+Encoding.ASCII.GetString(d,o+8,3).TrimEnd():""); if(n=="."||n=="..")continue; int c=U16(d,o+26);int sz=(int)U32(d,o+28);string full=prefix+"/"+n;
      if((d[o+11]&16)!=0){if(c>=2&&dirs.Add(c)){int count=0,x=c;var s=new HashSet<int>();while(x>=2&&s.Add(x)){count++;x=Next(fat,x,f12);if(x<2||x>=(f12?0xFF8:0xFFF8))break;}long co=ClOff(vol,bps,spc,first,c);long bytes=(long)count*bps*spc;if(count>0&&co>=vol&&co+bytes<=f.Length)Walk(f,vol,bps,spc,first,fat,f12,co,(int)bytes,full,find,extract,hits,dirs);}}
      else if(n.Equals(find,StringComparison.OrdinalIgnoreCase)){byte[] data=Chain(f,vol,bps,spc,first,fat,f12,c,sz);string hash=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(data)).Replace("-","");hits.Add(full+" size="+sz+" cluster="+c+" attr=0x"+d[o+11].ToString("X2")+" extracted="+data.Length+" sha256="+hash);if(!String.IsNullOrEmpty(extract))File.WriteAllBytes(extract,data);}
    }
  }
}
'@

Add-Type -TypeDefinition $source
[FatInspector]::Run($Image, $Find, $Extract)
