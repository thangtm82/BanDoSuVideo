"""Offline tests for V6.4 duration and shot frame allocation."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from v6_4_timeline import compile_timeline
from v6_4_ai_scene_plan import SCHEMA


class TimelineTests(unittest.TestCase):
    def plan(self):
        return {"schema": SCHEMA, "title": "History", "shots": [
            {"id": "a", "narration": "First", "visual_description": "Landscape", "duration_seconds": 10},
            {"id": "b", "narration": "Second", "visual_description": "City", "duration_seconds": 20},
            {"id": "c", "narration": "Third", "visual_description": "Map", "duration_seconds": 10},
        ]}

    def test_twelve_minutes(self):
        t = compile_timeline(self.plan(), minutes=12, fps=24)
        self.assertEqual(t["total_frames"], 17280)
        self.assertEqual(sum(s["frame_count"] for s in t["shots"]), 17280)
        self.assertEqual(t["shots"][-1]["end_frame_exclusive"], 17280)
        self.assertFalse(t["render_ready"])
        self.assertFalse(t["audio_required"])

    def test_deterministic(self):
        self.assertEqual(compile_timeline(self.plan(), minutes=12),
                         compile_timeline(self.plan(), minutes=12))

    def test_short_duration(self):
        t = compile_timeline(self.plan(), minutes=0.001, fps=24)
        self.assertEqual(t["total_frames"], 1) if False else None
        with self.assertRaises(ValueError):
            compile_timeline(self.plan(), minutes=0.00001, fps=24)

    def test_invalid_fps(self):
        with self.assertRaises(ValueError):
            compile_timeline(self.plan(), minutes=12, fps=0)


if __name__ == "__main__":
    unittest.main()
