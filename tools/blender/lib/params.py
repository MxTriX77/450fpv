"""Generator parameters (p1-pipeline-hardening, "Parameter sanity"; AST-03). Every generator declares a PARAMS table,
key -> {"type", "range", "default", "effect"}, nested keys dotted ("size_m.length"), which the object sheets'
generator tables mirror. build.py refuses a params file that strays from it before anything is built or exported:
an unknown key, a missing key, a wrong type or a number outside its range, each named with its range."""

TYPES = {"int": (int,), "float": (int, float), "str": (str,), "bool": (bool,)}


def validate(generator, params):
    """Problems with `params` against `generator.PARAMS`, as strings naming the key and its range; empty when they
    conform. Every declared key is required: the default documents the reference variant and is never filled in.
    A generator's optional check(params) adds its cross-parameter rules once every key is in range."""
    table = generator.PARAMS
    flat = _flatten(params)
    name = generator.__name__.rsplit(".", 1)[-1]
    problems = [f"unknown key '{k}' ({name} knows: {', '.join(table)})" for k in flat if k not in table]
    for key, spec in table.items():
        if key not in flat:
            problems.append(f"missing key '{key}' ({spec['type']}, {_range_text(spec)})")
        elif not _is_type(flat[key], spec["type"]):
            problems.append(f"'{key}' must be {spec['type']}, got {flat[key]!r}")
        elif spec["type"] in ("int", "float") and not spec["range"][0] <= flat[key] <= spec["range"][1]:
            problems.append(f"'{key}' = {flat[key]} is outside {_range_text(spec)}")
    if not problems and hasattr(generator, "check"):
        problems += generator.check(params)
    return problems


def _range_text(spec):
    r = spec["range"]
    return f"[{r[0]}, {r[1]}]" if isinstance(r, (tuple, list)) else str(r)


def _is_type(value, type_name):
    return isinstance(value, TYPES[type_name]) and (type_name == "bool" or not isinstance(value, bool))


def _flatten(value, prefix=""):
    if isinstance(value, dict) and value:
        flat = {}
        for k, v in value.items():
            flat.update(_flatten(v, f"{prefix}{k}."))
        return flat
    return {prefix[:-1]: value}
