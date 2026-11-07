"""Offline contract tests for AI scene planning (no API calls)."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from v6_4_ai_scene_plan import validate, generate, SCHEMA

class AiScenePlanTests(unittest.TestCase):
    def valid(self):
        return {"schema": SCHEMA, "title": "Test", "shots": [
            {"id": "shot-001", "narration": "A city", "visual_description": "Aerial city",
             "duration_seconds": 8}]}
    def test_valid_draft(self):
        self.assertEqual(validate(self.valid())["status"], "draft_requires_review")
    def test_missing_shots(self):
        with self.assertRaises(ValueError): validate({"schema": SCHEMA, "shots": []})
    def test_duplicate_ids(self):
        p = self.valid()
        p["shots"].append(dict(p["shots"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate"): validate(p)
    def test_invalid_duration(self):
        p = self.valid()
        p["shots"][0]["duration_seconds"] = -1
        with self.assertRaises(ValueError): validate(p)
    def test_missing_api_key(self):
        with self.assertRaisesRegex(ValueError, "Missing API key"):
            generate("script", endpoint="https://api.openai.com/v1/chat/completions",
                     model="test", key="")
if __name__ == "__main__":
    unittest.main()
