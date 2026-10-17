"""One module per generator, named in assets-src/catalog.json. Each exposes build(params) -> dict, a pure function
of the params (randomness only from params["seed"]), returning lib.geometry meshes per LOD and the physics metadata
in asset space (lib.frame); lib.meta.assemble documents the dict."""
