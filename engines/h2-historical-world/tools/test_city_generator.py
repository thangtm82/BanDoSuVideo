#!/usr/bin/env python3
"""Smoke tests for reproducible street-first V3 historical city layouts."""
import unittest
from city_generator import create_city

class CityGenerationTests(unittest.TestCase):
    def test_repeatable_layout_with_unique_ids(self):
        buildings, roads, events = create_city()
        self.assertEqual((buildings, roads, events), create_city())
        self.assertEqual(len({b['id'] for b in buildings}), len(buildings))
        self.assertGreater(len(roads), 5)
        self.assertGreater(len(buildings), 100)

    def test_epoch_landmarks_and_fortification(self):
        buildings, _, events = create_city()
        gates=[b for b in buildings if b['kind']=='gate']
        walls=[b for b in buildings if b['kind']=='wall']
        self.assertEqual(len(gates),4)
        self.assertGreater(len(walls),20)
        self.assertTrue(all(b['from']==1200 for b in gates+walls))
        self.assertTrue(any(b['kind']=='market' for b in buildings))
        self.assertTrue(any(b['kind']=='temple' for b in buildings))
        self.assertTrue(any(e['id']=='fortifications-completed' for e in events))

if __name__ == '__main__':
    unittest.main()
