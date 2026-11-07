"""Audio-first V6.4 timeline tests; no Blender or FFmpeg required."""
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from v6_4_audio_timeline import compile_timing


def plan():
    return {"schema": "h2.historical_world.scene_plan.v6.4",
            "shots": [
                {"id": "shot-001", "narration": "Một hai ba bốn năm.",
                 "visual_description": "Map", "duration_seconds": 10},
                {"id": "shot-002", "narration": "Một hai ba bốn năm sáu bảy tám.",
                 "visual_description": "City", "duration_seconds": 10}]}


class AudioTimelineTests(unittest.TestCase):
    def test_text_fallback_is_contiguous_and_not_forced_to_project_length(self):
        result = compile_timing(plan(), fps=24, project_minutes=12)
        self.assertEqual(result["timing_mode"], "audio_first_text_fallback")
        self.assertEqual(result["shots"][0]["timing_source"], "text_estimate")
        self.assertEqual(result["shots"][1]["start_frame"], result["shots"][0]["end_frame_exclusive"])
        self.assertEqual(result["shots"][-1]["end_frame_exclusive"], result["total_frames"])
        self.assertNotEqual(result["total_frames"], result["target_project_frames"])

    def test_audio_overrides_text_only_for_matching_shot(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "shot-001.mp3").write_bytes(b"mock")
            with patch("v6_4_audio_timeline.probe_duration", return_value=7.25) as probe:
                result = compile_timing(plan(), fps=24, audio_dir=d)
            probe.assert_called_once()
            self.assertEqual(result["shots"][0]["timing_source"], "audio")
            self.assertEqual(result["shots"][0]["frame_count"], 186)
            self.assertEqual(result["shots"][1]["timing_source"], "text_estimate")

    def test_duplicate_audio_is_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "shot-001.mp3").write_bytes(b"x")
            (Path(d) / "shot-001.wav").write_bytes(b"x")
            with self.assertRaises(ValueError):
                compile_timing(plan(), audio_dir=d)


if __name__ == "__main__":
    unittest.main()
