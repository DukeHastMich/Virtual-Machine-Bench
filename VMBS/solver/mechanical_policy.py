def classify(footprint,inside):return "FIXED_EDGE" if "Edge" in footprint else ("MOVABLE" if inside else "REVIEW")
