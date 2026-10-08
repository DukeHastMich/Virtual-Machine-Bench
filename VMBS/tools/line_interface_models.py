"""Manual-backed digital projection of classic EIA-232 interface ICs.
CP63. Analog thresholds, slew rate, loading and supply behavior stay in electrical evidence.
"""
class MC1488:
    """Quad TTL/DTL -> EIA-232 driver. Output is voltage polarity: +1 positive, -1 negative."""
    channels=4
    @staticmethod
    def drive(ttl_level:int)->int:
        if ttl_level not in (0,1): raise ValueError('TTL input must be 0/1')
        return 1 if ttl_level==0 else -1

class MC1489:
    """Quad EIA-232 -> TTL receiver digital projection. Input is line polarity."""
    channels=4
    @staticmethod
    def receive(line_polarity:int)->int:
        if line_polarity not in (-1,1): raise ValueError('line polarity must be -1/+1')
        return 0 if line_polarity==1 else 1
