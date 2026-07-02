-- MUD world expansion: Outer Ward (Medieval) and Drift Ring (Sci-Fi) zones,
-- lore figures, crafting resources, and re-seed of missing base ingredients.

-- ── Zones ────────────────────────────────────────────────────────────────────

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Outer Ward', 'outer-ward', 'The working ground between the keep gate and the outer wall: yards, kennels, gardens, and trades that kept the keep alive.', 4)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Drift Ring', 'drift-ring', 'The lower service ring of Star Reach, where cargo, drones, growing racks, and quiet broadcast vaults ride beneath the main decks.', 5)
ON CONFLICT (slug) DO NOTHING;

-- ── Outer Ward rooms ─────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Bailey Yard', 'bailey-yard',
       'A packed-earth training yard flanked by straw dummies and a rack of blunted arms. Boot prints overlap in every direction, none of them recent.',
       0, 0, 0
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Gate Tower', 'gate-tower',
       'A cramped watch chamber above the outer gate. Arrow slits frame thin blades of daylight, and the wind carries the smell of rain off the fields.',
       1, 0, -2
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Kennel Row', 'kennel-row',
       'Low timber kennels line a straw-strewn lane. The gates hang open and the water troughs are dry, but something still turns circles in the far pen at dusk.',
       2, -2, 0
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Herb Garden', 'herb-garden',
       'Raised beds of sage, yarrow, and feverfew grow half-wild inside a wattle fence. Someone has kept one corner weeded, recently and with care.',
       3, -2, -2
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Old Well', 'old-well',
       'A stone wellhead stands at the center of a worn court. The rope is new, the bucket is patched, and the water below sounds deeper than it should.',
       4, 0, 2
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Tannery Lane', 'tannery-lane',
       'Curing racks and lime barrels crowd a narrow work lane. The sharp smell keeps most visitors moving, which suits whoever still works here.',
       5, -2, 2
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Drift Ring rooms ─────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Ring Junction', 'ring-junction',
       'A circular junction where the ring corridors meet under banked guide lights. Route arrows still rotate on the floor plating, patient as tide marks.',
       0, 0, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Cargo Spine', 'cargo-spine',
       'A long rack corridor of clamped crates and rail-mounted lifters. Half the containers are tagged for destinations that no longer answer.',
       1, 2, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Drone Foundry', 'drone-foundry',
       'Assembly cradles hang over a print floor dusted with alloy powder. Unfinished drone frames wait in ranks, each one stopped mid-becoming.',
       2, 2, 2
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Botany Loop', 'botany-loop',
       'Stacked growing racks curve along the ring wall under violet lamps. Condensation beads and falls somewhere out of sight, keeping its own slow time.',
       3, -2, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Medbay Annex', 'medbay-annex',
       'A small triage annex of fold-out beds and supply lockers. The diagnostic table still glows faintly, ready for a patient who is very late.',
       4, -2, 2
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Broadcast Vault', 'broadcast-vault',
       'A shielded vault of archived transmission cores. Every rack whispers at the edge of hearing, as if the old broadcasts refuse to end completely.',
       5, 0, 2
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Outer Ward exits ─────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Out to the bailey yard', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
WHERE r1.slug = 'keep-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the keep gate', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'keep-gate'
WHERE r1.slug = 'bailey-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Ladder up the gate tower', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'gate-tower'
WHERE r1.slug = 'bailey-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the yard', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
WHERE r1.slug = 'gate-tower'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Along kennel row', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'kennel-row'
WHERE r1.slug = 'bailey-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the yard', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
WHERE r1.slug = 'kennel-row'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Through the garden gate', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'herb-garden'
WHERE r1.slug = 'kennel-row'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to kennel row', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'kennel-row'
WHERE r1.slug = 'herb-garden'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Across to the herb garden', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'herb-garden'
WHERE r1.slug = 'gate-tower'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Across to the gate tower', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'gate-tower'
WHERE r1.slug = 'herb-garden'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the old well', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-well'
WHERE r1.slug = 'bailey-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the yard', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
WHERE r1.slug = 'old-well'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Into tannery lane', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'tannery-lane'
WHERE r1.slug = 'old-well'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the well court', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-well'
WHERE r1.slug = 'tannery-lane'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Drift Ring exits ─────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Freight lift to the drift ring', 'elevator'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'ring-junction'
WHERE r1.slug = 'salvage-bay'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Freight lift to the salvage bay', 'elevator'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'salvage-bay'
WHERE r1.slug = 'ring-junction'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Into the cargo spine', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cargo-spine'
WHERE r1.slug = 'ring-junction'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the junction', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'ring-junction'
WHERE r1.slug = 'cargo-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down into the drone foundry', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'drone-foundry'
WHERE r1.slug = 'cargo-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the cargo spine', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cargo-spine'
WHERE r1.slug = 'drone-foundry'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Around to the botany loop', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'botany-loop'
WHERE r1.slug = 'ring-junction'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the junction', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'ring-junction'
WHERE r1.slug = 'botany-loop'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Through to the medbay annex', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'medbay-annex'
WHERE r1.slug = 'botany-loop'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the botany loop', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'botany-loop'
WHERE r1.slug = 'medbay-annex'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Sealed access to the broadcast vault', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'broadcast-vault'
WHERE r1.slug = 'ring-junction'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the junction', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'ring-junction'
WHERE r1.slug = 'broadcast-vault'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Outer Ward lore figures ──────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Drill Master', 'drill-master',
       'A scarred drill master leans on a blunted polearm, correcting the stance of soldiers who left years ago.',
       'The drill master grunts, "Twine, flint, and dry tallow won more sieges than speeches did. Kit first. Glory after."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'bailey-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'drill-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Watch Horn', 'watch-horn',
       'A cracked signal horn hangs by the arrow slit on a braided cord, polished bright where generations of hands gripped it.',
       'Scratched under the mount: ONE BLAST FRIEND, TWO BLASTS TRADE, THREE BLASTS BAR THE GATE AND PRAY.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'gate-tower'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'watch-horn' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Kennel Master', 'kennel-master',
       'A whistling kennel master mends a lead by lantern light, glancing at the empty pens as if counting heads only she can see.',
       'She says without looking up, "Feed what guards you before you feed yourself. That rule kept this ward alive longer than its walls did."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'kennel-row'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'kennel-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hedge Witch', 'hedge-witch',
       'A hedge witch in a seed-hung shawl moves along the beds, talking to the plants in the tone most people save for children.',
       'She presses a leaf flat and says, "Herb and clean water will mend more than any blade ever unmade. Steep, wrap, rest. In that order."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'herb-garden'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hedge-witch' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Well Windlass', 'well-windlass',
       'The well windlass is carved with names, dates, and small warnings from everyone who ever drew water here.',
       'Among the carvings, one line is cut deeper than the rest: DRINK BEFORE THE VAULTS. NOTHING DOWN THERE SHARES.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'old-well'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'well-windlass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Old Tanner', 'old-tanner',
       'An old tanner works a hide across a beam with slow, exact strokes, unbothered by the smell or the silence.',
       'He nods at the racks. "Good strap outlasts good intentions. Cut it honest, oil it often, and it will hold when you cannot."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'tannery-lane'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'old-tanner' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Drift Ring lore figures ──────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Traffic Oracle', 'traffic-oracle',
       'A routing pillar wakes as you approach, projecting lane maps for traffic that stopped flowing long ago.',
       'The oracle recites: "Cargo east. Growth west. Memory south. The ring keeps every route open, in case anyone remembers where they were going."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'ring-junction'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'traffic-oracle' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cargo Tally', 'cargo-tally',
       'A wall-mounted tally board flickers through manifests, pausing on entries flagged in patient amber.',
       'Flagged entry: crate 88-C, magnetic clamps, surplus. Note from the last quartermaster: TAKE SPARES. THE RING GIVES NOTHING TWICE.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'cargo-spine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cargo-tally' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Foundry Mind', 'foundry-mind',
       'The foundry control core watches from its cradle, still holding the pattern of every drone it never finished.',
       'The mind hums: "A frame without a core is furniture. A core without a shell is a rumor. Bring both, and I will call it flight."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'drone-foundry'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'foundry-mind' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Garden Automaton', 'garden-automaton',
       'A soft-handed automaton tends the growing racks, misting leaves and adjusting lamps with unhurried devotion.',
       'It says gently, "Gel feeds the body. Gauze binds the wound. The station grew both because people insisted on surviving."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'botany-loop'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'garden-automaton' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Triage Specter', 'triage-specter',
       'A translucent medical overlay paces the annex, checking beds that hold nothing but folded blankets.',
       'The specter recites the old rule: "Stabilize, then move. Clean, then close. Hope is a supply like any other. Restock it."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'medbay-annex'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'triage-specter' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Vault Chorus', 'vault-chorus',
       'The archived transmission cores murmur together at the edge of hearing, a chorus of every message the station ever refused to lose.',
       'One voice separates from the rest: "Record what you build. Tape remembers what pride forgets."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'broadcast-vault'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vault-chorus' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Outer Ward resources ─────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hemp Twine', 'hemp-twine',
       'A tight coil of waxed hemp twine, strong enough to lash gear or bind a handle.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'bailey-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hemp-twine' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Flint Shard', 'flint-shard',
       'A palm-sized flint shard with a clean striking edge, kept dry beside the watch brazier.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'gate-tower'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'flint-shard' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Tallow Cake', 'tallow-cake',
       'A dense cake of rendered tallow, good for waterproofing, greasing, or feeding a slow flame.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'kennel-row'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'tallow-cake' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Dried Herbs', 'dried-herbs',
       'A tied bundle of sage, yarrow, and feverfew, dried whole and still sharp with scent.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'herb-garden'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dried-herbs' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Spring Water', 'spring-water',
       'A stoppered flask of cold well water, drawn deep and clear.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'old-well'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'spring-water' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Leather Strap', 'leather-strap',
       'A supple cured strap, cut honest and oiled against the weather.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'tannery-lane'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'leather-strap' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Drift Ring resources ─────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Beacon Shell', 'beacon-shell',
       'An empty beacon casing with intact mounts and a clear lens, waiting for a working heart.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'ring-junction'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'beacon-shell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mag Clamp', 'mag-clamp',
       'A surplus magnetic clamp from crate 88-C, still strong enough to grip through a glove.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'cargo-spine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mag-clamp' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Servo Core', 'servo-core',
       'A sealed drone servo core, its status ring still breathing a slow standby pulse.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'drone-foundry'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'servo-core' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Nutrient Gel', 'nutrient-gel',
       'A pouch of pale nutrient gel, formulated to keep either a crop or a crew member going.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'botany-loop'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'nutrient-gel' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Sterile Gauze', 'sterile-gauze',
       'A vacuum-sealed roll of sterile gauze from the annex lockers, untouched since the last drill.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'medbay-annex'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'sterile-gauze' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cipher Tape', 'cipher-tape',
       'A spool of archival cipher tape, dense with encoded broadcast fragments.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'broadcast-vault'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cipher-tape' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Re-seed missing base crafting ingredients ────────────────────────────────
-- These were single-instance items that players picked up; without them the
-- torch, signal-key, forge-wrap, and patch-cable recipes cannot be crafted by
-- anyone else. Idempotent: inserts only when absent from the home room.

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rag Strip', 'rag-strip',
       'A strip of old cloth torn from a supply wrap. It would burn quickly if soaked in oil.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'archive-hall'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rag-strip' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Lamp Oil', 'lamp-oil',
       'A stoppered flask of lamp oil. Enough for a small torch or signal burn.',
       NULL, true, 1
FROM mud_rooms r
WHERE r.slug = 'heartbeat-room'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lamp-oil' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Wire Spool', 'wire-spool',
       'A short spool of thin signaling wire, still dry and tightly wound.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'observatory'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'wire-spool' AND i.room_id = r.id AND i.owner_character_id IS NULL);
