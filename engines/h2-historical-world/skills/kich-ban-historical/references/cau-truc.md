# Historical World structure
Engine: `historical`; timeline: `story/shots.json`; world: `history/world.json`.

```json
{
  "epochs": [{"id":"early","year":1000}, {"id":"late","year":1200}],
  "buildings": [{"id":"house-1","kind":"house","p":[0,0],"from":1000,"to":1200}],
  "world": {"size":1400,"ground":"#a1a287","hills":[],"vegetation":{"seed":1,"count":200}}
}
```

Every historical shot is `{"id":"s001","start":0,"end":8,"line":"...","tpl":"epoch3d","args":{"epoch":"early","camera":{"from":[680,650,700],"to":[500,430,600],"target":[0,10,0]}}}`.

All coordinates are **local illustrative units** until a verified mapping to real geographic data exists. Evidence must be checked by the human author.
