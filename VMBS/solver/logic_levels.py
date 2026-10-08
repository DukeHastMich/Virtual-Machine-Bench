def check(voh,vol,vih,vil):return {"high_margin":voh-vih,"low_margin":vil-vol,"pass":voh>=vih and vol<=vil}
