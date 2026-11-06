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
        self.assertTrue(all(isinstance(r, dict) and 'from' in r for r in roads))
        self.assertEqual(sum(1 for r in roads if r['from']==1000),2)

    def test_style_variety_and_structural_clearance(self):
        from city_generator import distance_to_polyline
        buildings, roads, _ = create_city()
        houses=[b for b in buildings if b['kind']=='house']
        styles={b['variant'] for b in houses}
        self.assertGreaterEqual(len(styles),6)
        self.assertTrue(all(distance_to_polyline(b['p'])>=32 for b in houses))
        self.assertEqual(len({b['id'] for b in buildings}),len(buildings))

    def test_epoch_landmarks_and_fortification(self):
        buildings, _, events = create_city()
        gates=[b for b in buildings if b['kind']=='gate']
        walls=[b for b in buildings if b['kind']=='wall']
        self.assertEqual(len(gates),4)
        self.assertGreater(len(walls),20)
        self.assertTrue(all(b['from']==1200 for b in gates+walls))
        self.assertTrue(any(b['kind']=='market' for b in buildings))
        self.assertTrue(any(b['kind']=='temple' for b in buildings))
        self.assertTrue(any(e['id']=='fortification-completed' for e in events))

if __name__ == '__main__':
    unittest.main()
