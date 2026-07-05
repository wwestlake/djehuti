-- MUD realm expansion: The Veil, The Wild March, The Drowned Reach.
-- No schema changes: uses existing mud_zones.realm_slug, mud_rooms,
-- mud_exits exactly as the medieval/sci-fi realms already do.

-- ── Zones ────────────────────────────────────────────────────────────────────

INSERT INTO mud_zones (name, slug, description, position, realm_slug)
VALUES ('The Veil', 'the-veil', 'A liminal, fractured dimension of shifting geometry and decaying industrial architecture, where shadowed alleys are sliced by jagged beams of ethereal light.', 8, 'the-veil')
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position, realm_slug)
VALUES ('The Wild March', 'the-wild-march', 'An untamed, highly vertical frontier where aggressive flora reclaims ancient ruins, giant roots and cascading vines weaving natural pathways over and under one another.', 9, 'the-wild-march')
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position, realm_slug)
VALUES ('The Drowned Reach', 'the-drowned-reach', 'A submerged, abyssal environment of crushing pressure and aquatic decay: interconnected underwater facilities, sunken caverns, and air-locked habitats.', 10, 'the-drowned-reach')
ON CONFLICT (slug) DO NOTHING;

-- ── The Veil rooms ───────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'The First Tear', 'veil-first-tear',
       'A tear in the heavy, textured air glows with harsh, cool light, opening onto a fractured street of scrap metal and crumbling concrete. Twisted lampposts lean at impossible angles, and thick, hanging fog swallows the middle distance in industrial gray. Somewhere close, a jagged beam of ethereal light slices clean through the gloom.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'the-veil'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Claustrophobic Alley', 'veil-claustrophobic-alley',
       'A narrow, claustrophobic alleyway hemmed in by looming, abstract concrete walls rendered in heavy, palette-knife strokes. Rusted iron fire escapes jut overhead at asymmetrical angles, and shattered glass crunches underfoot, catching stray flickers of stark, contrasting neon.',
       1, 0, 1
FROM mud_zones z WHERE z.slug = 'the-veil'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'The Frayed Cage Lift', 'veil-frayed-cage-lift',
       'A rusted, exposed-cage lift groans on exposed gears and frayed cables, hanging in a fractured stairwell that seems to lead nowhere and everywhere. Static haze presses against the cage bars, and the archway framing it is asymmetrical, as though the geometry here forgot how to agree with itself.',
       2, 0, 2
FROM mud_zones z WHERE z.slug = 'the-veil'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── The Wild March rooms ─────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Greatroot Landing', 'march-greatroot-landing',
       'Massive timber roots the width of towers plunge into weather-beaten stone below, forming a natural landing beneath an overarching canopy. Dappled emerald light pierces the humid air, thick with floating spores, and bioluminescent moss traces faint blue-green veins across petrified wood.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'the-wild-march'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'The Hollow Trunk', 'march-hollow-trunk',
       'An enormous hollowed-out tree trunk rises overhead, its interior lit by dappled green light filtering through knotholes far above. Cascading vines weave over and under one another in a repeating, rhythmic lattice, and the air smells of wet bark and drifting spores.',
       1, 0, 1
FROM mud_zones z WHERE z.slug = 'the-wild-march'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Vine-Swallowed Altar', 'march-vine-swallowed-altar',
       'An ancient stone altar, weather-beaten and swallowed by creeping vines, sits beneath a natural bridge of intertwined roots. Bioluminescent moss pulses faintly in the humid gloom, and petrified wood grain shows through where the vines have not yet claimed it.',
       2, 0, 2
FROM mud_zones z WHERE z.slug = 'the-wild-march'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── The Drowned Reach rooms ──────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'First Airlock', 'reach-first-airlock',
       'A heavy, circular airlock with a central locking wheel dominates the bulkhead, ringed by flickering warning lights. Riveted steel groans faintly under crushing pressure, and beyond the barnacle-encrusted glass porthole, extremely low visibility swallows everything past arm''s reach.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'the-drowned-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Grated Descent', 'reach-grated-descent',
       'A grated, metal spiral staircase descends into dark, knee-high pooling water, lit only by a sickly-yellow maritime lamp bolted to a dripping pipe network. Condensation slicks every rail, and heavy brass fittings show the dull green of long submersion.',
       1, 0, 1
FROM mud_zones z WHERE z.slug = 'the-drowned-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'The Glass Tunnel', 'reach-glass-tunnel',
       'A cylindrical, reinforced glass tunnel shows the murky ocean exterior pressing in on all sides, deep-sea bioluminescence drifting past like slow embers. Massive pressure valves line the curved walls beside a damp, condensation-slicked control console.',
       2, 0, 2
FROM mud_zones z WHERE z.slug = 'the-drowned-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Intra-realm exits ────────────────────────────────────────────────────────

-- The Veil: Tear -> Alley -> Lift, with returns
INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Into the claustrophobic alley', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'veil-claustrophobic-alley'
WHERE r1.slug = 'veil-first-tear'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back through the tear', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'veil-first-tear'
WHERE r1.slug = 'veil-claustrophobic-alley'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the frayed cage lift', 'elevator'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'veil-frayed-cage-lift'
WHERE r1.slug = 'veil-claustrophobic-alley'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the alley', 'elevator'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'veil-claustrophobic-alley'
WHERE r1.slug = 'veil-frayed-cage-lift'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- The Wild March: Landing -> Hollow Trunk -> Altar, with returns
INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Into the hollow trunk', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'march-hollow-trunk'
WHERE r1.slug = 'march-greatroot-landing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the landing', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'march-greatroot-landing'
WHERE r1.slug = 'march-hollow-trunk'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Up the bark-carved steps', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'march-vine-swallowed-altar'
WHERE r1.slug = 'march-hollow-trunk'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Down into the hollow trunk', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'march-hollow-trunk'
WHERE r1.slug = 'march-vine-swallowed-altar'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- The Drowned Reach: Airlock -> Grated Descent -> Glass Tunnel, with returns
INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Down the grated stair', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'reach-grated-descent'
WHERE r1.slug = 'reach-first-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Up to the airlock', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'reach-first-airlock'
WHERE r1.slug = 'reach-grated-descent'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Into the glass tunnel', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'reach-glass-tunnel'
WHERE r1.slug = 'reach-grated-descent'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the grated stair', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'reach-grated-descent'
WHERE r1.slug = 'reach-glass-tunnel'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Portals to and from the Threshold of Realms ─────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'the-veil', 'A tear in the air, glowing with harsh, cool light', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'veil-first-tear'
WHERE r1.slug = 'threshold-of-realms'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'portal', 'Return to the threshold', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'threshold-of-realms'
WHERE r1.slug = 'veil-first-tear'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'the-wild-march', 'A colossal, overgrown archway of woven, calcified branches', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'march-greatroot-landing'
WHERE r1.slug = 'threshold-of-realms'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'portal', 'Return to the threshold', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'threshold-of-realms'
WHERE r1.slug = 'march-greatroot-landing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'the-drowned-reach', 'A heavy, circular vault door, dripping and cold', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'reach-first-airlock'
WHERE r1.slug = 'threshold-of-realms'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'portal', 'Return to the threshold', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'threshold-of-realms'
WHERE r1.slug = 'reach-first-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;
