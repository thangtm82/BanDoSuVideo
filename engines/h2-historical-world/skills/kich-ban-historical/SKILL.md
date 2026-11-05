# Kịch bản H2 Historical World 3D

Use this authoring reference only with the `historical` pack. Create narration and a plan for epochs and buildings on a **single stable world**. Preserve provided timecodes, quote sources for all historical facts, and label uncertain architecture or geography as illustrative.

Before any AI-generated file is accepted:
- Determine world bounds and its stable local coordinate system.
- List epochs in strictly increasing years and keep stable IDs.
- Identify buildings and lifetime ranges `from <= year < to`; do not invent factual coordinates.
- Describe each shot's `args.epoch` and `args.camera` using `from`, `to` and `target` numeric vectors.
- Do not change voice text or timecodes unless explicitly requested.
- Distinguish demonstrative fictional cities from researched historical reconstructions.

The initial engine generates demonstration data only; the C# AI Historical Builder remains a future milestone.
