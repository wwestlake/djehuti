-- MUD world expansion 3 ("the double"): four new zones, two per realm.
-- Medieval: Hollow Hills (faerie under-hill) and Beacon Crag (mountain heights).
-- Sci-Fi: Signal Sea (data realm inside the archived broadcasts) and
-- Outer Hull (EVA surface of the Vagrant Star, including The Scar).

-- ── Zones ────────────────────────────────────────────────────────────────────

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Hollow Hills', 'hollow-hills', 'The under-hill country behind the shrine, where barrow doors open on root-vaulted halls, twilight bargains, and sleepers who are not done yet.', 8)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Beacon Crag', 'beacon-crag', 'The bare heights above the Greenwood: scree, wind, a cold signal beacon, and a ring of standing stones that watches the sky back.', 9)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Signal Sea', 'signal-sea', 'The inside of the archive: a navigable sea of old transmissions where signal washes ashore as sound, light, and almost-people.', 10)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Outer Hull', 'outer-hull', 'The skin of the Vagrant Star, walked in mag-boots under naked stars: antenna groves, solar vanes, and the scar of whatever was faster.', 11)
ON CONFLICT (slug) DO NOTHING;

-- ── Hollow Hills rooms ───────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Barrow Gate', 'barrow-gate',
       'Behind the shrine, a turf door stands open in the oldest mound, its lintel worn smooth by hands that mostly knocked politely. The air inside smells of rain that fell centuries ago.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hollow Court', 'hollow-court',
       'A hall vaulted by living roots, lit by amber light with no visible source. At its center stands an empty throne of woven willow that everyone present is careful not to face away from.',
       1, -2, 0
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Twilight Market', 'twilight-market',
       'Stalls of woven shadow sell things that should not be for sale: a jar of first snow, a spool of borrowed time, someone''s carefully labeled regret. Prices are never written down.',
       2, -2, -2
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Sleepers'' Barrow', 'sleepers-barrow',
       'Twelve armored figures lie on stone biers around a fire that burns cold and blue. Their armor is out of date by four hundred years. Their swords are not dusty.',
       3, 0, 2
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Echo Pool', 'echo-pool',
       'A black pool fills the deepest chamber, perfectly still. It does not reflect your face. It repeats, very quietly, things you said in rooms far away from here.',
       4, -4, 0
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Candle Warren', 'candle-warren',
       'Low tunnels branch and rejoin, lit by candles that stand in wall niches and burn without shrinking. Where the candles stop, the warren keeps going. Nobody follows the dark branch twice.',
       5, 0, -2
FROM mud_zones z WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Beacon Crag rooms ────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Scree Path', 'scree-path',
       'The trees give up and the mountain begins: a switchback path over loose slate, marked by cairns that someone rebuilds after every storm.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hermit Cell', 'hermit-cell',
       'A dry stone cell leans into the mountainside, half dwelling and half apiary. Bees work the heather in defiance of the altitude, and the shelf inside holds more books than bowls.',
       1, -2, 0
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Beacon Platform', 'beacon-platform',
       'A great iron brazier crowns the crag, scoured clean and stacked with coal under oilcloth. It has been ready to burn for a generation. It is waiting for a specific reason.',
       2, 0, -2
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Eyrie Ledge', 'eyrie-ledge',
       'A wind-hammered ledge overhangs the whole valley. White falcons nest in the crag face and regard visitors with the professional interest of landlords.',
       3, 2, -2
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Cloud Shelf', 'cloud-shelf',
       'Above the weather now: a broad stone shelf where the cloud tops spread below like a second country. Sound arrives late up here, and some of it never arrives at all.',
       4, 0, -4
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Star Ring', 'star-ring',
       'Nine standing stones crown the summit, each one drilled with a sighting hole aimed at a different piece of sky. Standing in the center, you get the distinct impression of being sighted back.',
       5, 2, -4
FROM mud_zones z WHERE z.slug = 'beacon-crag'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Signal Sea rooms ─────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Static Shore', 'static-shore',
       'You stand on a beach of soft white noise under a sky the color of an idle screen. Waves of half-decoded audio break and hiss out. Somewhere behind you is the way back up.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Packet Reef', 'packet-reef',
       'Bright lattices of routed data grow like coral, splitting and recombining in slow pulses. Small quick things dart between the branches, carrying fragments somewhere important.',
       1, 2, 0
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Archive Atoll', 'archive-atoll',
       'A ring of catalogued islands, each one a filed decade. The lagoon in the middle is perfectly indexed and refuses to say what it is an index of.',
       2, 4, 0
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Carrier Wave', 'carrier-wave',
       'A standing wave rolls forever without breaking, broad enough to walk on. Riding it, you can feel the whole sea''s traffic pass under your feet like a train through a floor.',
       3, 0, -2
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Ghost Frequency', 'ghost-frequency',
       'A quiet band between stations. Something broadcasts here on a loop — music, maybe, or a voice, always just below recognition — patient as a lighthouse with no coast.',
       4, 2, -2
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Dead Channel', 'dead-channel',
       'The sea ends here in a flat, silent expanse where nothing transmits. It is not empty. It is the kind of quiet that has been swept and kept.',
       5, -2, 0
FROM mud_zones z WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Outer Hull rooms ─────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hull Gangway', 'hull-gangway',
       'Mag-boots click onto the Vagrant Star''s skin. Handhold lines run fore and aft under a sky with no up in it, and the station''s ring light lies across the plating like shallow water.',
       0, 0, 0
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Dorsal Spine', 'dorsal-spine',
       'The long ridge of the ship runs forward in bolted segments. A maintenance track follows it, polished bright by something that still makes its rounds.',
       1, 0, -2
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'The Scar', 'the-scar',
       'The hull folds inward here in one long, clean stroke — not an explosion, not a collision, a cut. The edges have fused to dark glass. Nothing about the geometry of it is comfortable.',
       2, 0, -4
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Antenna Forest', 'antenna-forest',
       'Masts, dishes, and whip aerials crowd this stretch of hull like a winter wood. When the station''s beacon sweeps past, the whole forest hums one low chord.',
       3, 2, 0
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Solar Vane', 'solar-vane',
       'A great foil vane extends from the hull, patched and repatched, still drinking starlight. Walking its spar feels like crossing a bridge made of bright paper.',
       4, 4, 0
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Stargazer Blister', 'stargazer-blister',
       'A transparent observation blister swells from the hull at the vane''s root. Inside, one worn couch faces out at everything. Someone loved this seat. The wear says so.',
       5, 4, -2
FROM mud_zones z WHERE z.slug = 'outer-hull'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Hollow Hills exits ───────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Through the turf door under the mound', 'door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
WHERE r1.slug = 'shrine-clearing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back out to the shrine clearing', 'door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'shrine-clearing'
WHERE r1.slug = 'barrow-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Down into the hollow court', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hollow-court'
WHERE r1.slug = 'barrow-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back toward the barrow gate', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
WHERE r1.slug = 'hollow-court'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Into the candle warren', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'candle-warren'
WHERE r1.slug = 'barrow-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to the barrow gate', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
WHERE r1.slug = 'candle-warren'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down among the sleepers', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'sleepers-barrow'
WHERE r1.slug = 'barrow-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up from the sleepers'' hall', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
WHERE r1.slug = 'sleepers-barrow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up into the twilight market', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'twilight-market'
WHERE r1.slug = 'hollow-court'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back down to the court', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hollow-court'
WHERE r1.slug = 'twilight-market'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Deeper, to the echo pool', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'echo-pool'
WHERE r1.slug = 'hollow-court'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back toward the court''s amber light', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hollow-court'
WHERE r1.slug = 'echo-pool'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Beacon Crag exits ────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up the scree path above the trees', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'scree-path'
WHERE r1.slug = 'watch-oak'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the watch oak', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'watch-oak'
WHERE r1.slug = 'scree-path'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Along the ledge to the hermit cell', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hermit-cell'
WHERE r1.slug = 'scree-path'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the scree path', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'scree-path'
WHERE r1.slug = 'hermit-cell'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Climb to the beacon platform', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'beacon-platform'
WHERE r1.slug = 'scree-path'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Descend to the scree path', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'scree-path'
WHERE r1.slug = 'beacon-platform'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Out along the eyrie ledge', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'eyrie-ledge'
WHERE r1.slug = 'beacon-platform'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back from the ledge', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'beacon-platform'
WHERE r1.slug = 'eyrie-ledge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up onto the cloud shelf', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cloud-shelf'
WHERE r1.slug = 'beacon-platform'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the beacon platform', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'beacon-platform'
WHERE r1.slug = 'cloud-shelf'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Across to the star ring', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'star-ring'
WHERE r1.slug = 'cloud-shelf'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the cloud shelf', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cloud-shelf'
WHERE r1.slug = 'star-ring'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Signal Sea exits ─────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'dive', 'Dive into the archived signal', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'static-shore'
WHERE r1.slug = 'broadcast-vault'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'surface', 'Surface back to the broadcast vault', 'portal'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'broadcast-vault'
WHERE r1.slug = 'static-shore'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Wade out to the packet reef', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'packet-reef'
WHERE r1.slug = 'static-shore'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the static shore', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'static-shore'
WHERE r1.slug = 'packet-reef'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Out to the archive atoll', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'archive-atoll'
WHERE r1.slug = 'packet-reef'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back along the reef', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'packet-reef'
WHERE r1.slug = 'archive-atoll'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Climb onto the carrier wave', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'carrier-wave'
WHERE r1.slug = 'static-shore'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Slide back down to the shore', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'static-shore'
WHERE r1.slug = 'carrier-wave'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Ride toward the ghost frequency', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'ghost-frequency'
WHERE r1.slug = 'carrier-wave'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back onto the carrier wave', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'carrier-wave'
WHERE r1.slug = 'ghost-frequency'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Walk out onto the dead channel', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'dead-channel'
WHERE r1.slug = 'static-shore'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back toward the noise of the shore', 'passage'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'static-shore'
WHERE r1.slug = 'dead-channel'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Outer Hull exits ─────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'eva', 'Cycle out onto the hull', 'bulkhead'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hull-gangway'
WHERE r1.slug = 'wreck-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'airlock', 'Cycle back inside the wreck', 'bulkhead'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'wreck-airlock'
WHERE r1.slug = 'hull-gangway'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Clip on and walk the dorsal spine', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'dorsal-spine'
WHERE r1.slug = 'hull-gangway'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back along the spine to the gangway', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hull-gangway'
WHERE r1.slug = 'dorsal-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Forward to the scar', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'the-scar'
WHERE r1.slug = 'dorsal-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Away from the scar, back down the spine', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'dorsal-spine'
WHERE r1.slug = 'the-scar'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Into the antenna forest', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'antenna-forest'
WHERE r1.slug = 'hull-gangway'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Out of the aerials, back to the gangway', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hull-gangway'
WHERE r1.slug = 'antenna-forest'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Out along the solar vane''s spar', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'solar-vane'
WHERE r1.slug = 'antenna-forest'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back off the vane', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'antenna-forest'
WHERE r1.slug = 'solar-vane'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the stargazer blister', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'stargazer-blister'
WHERE r1.slug = 'solar-vane'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the vane spar', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'solar-vane'
WHERE r1.slug = 'stargazer-blister'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Hollow Hills lore figures ────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Door Knocker', 'door-knocker',
       'The turf door bears a brass knocker cast as a sleeping face. It is not sleeping. It is being polite.',
       'The face opens one eye. "Rules of the hill: give your name freely and it stays yours. Trade it, and it does not. Knock on your way out, so we know the hill let you go."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'barrow-gate'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'door-knocker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hollow Herald', 'hollow-herald',
       'A slight figure in moth-wing livery stands beside the empty willow throne, announcing each visitor to nobody with complete conviction.',
       'The herald taps a staff twice. "The Court is Out. The Court is expected. All debts, dances, and durances remain enforceable in the Court''s absence. Do enjoy the market."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'hollow-court'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hollow-herald' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Bargain Crow', 'bargain-crow',
       'A crow in a tiny merchant''s cap keeps a stall of shining oddments, weighing customers with one eye and their pockets with the other.',
       'The crow clicks its beak. "Fair warning, walker: everything here costs exactly what it says. The trick is that nothing says. Silk for the careful. Buttons for the brave."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'twilight-market'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'bargain-crow' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Eldest Sleeper', 'eldest-sleeper',
       'The knight on the central bier wears a crown of tarnished silver. Frost gathers on the armor and melts in slow cycles, as if the sleeper dreams of seasons.',
       'Carved along the bier: WAKE US FOR THE BELL, THE BEACON, OR THE THIRD THING. NOT FOR WARS. WE HAVE SEEN THE WARS. THEY KEEP.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'sleepers-barrow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'eldest-sleeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Borrowed Voice', 'borrowed-voice',
       'Something in the pool speaks only in words it has collected from visitors, rearranged with unsettling care.',
       'The pool says, in a voice that is almost yours: "You said — coming back — you said — safe. The pool keeps. The pool returns. Leave a word, take a word."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'echo-pool'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'borrowed-voice' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Candle Keeper', 'candle-keeper',
       'A stooped figure moves through the warren trimming wicks that never burn down, humming a counting song with no highest number.',
       'The keeper holds up a candle. "Every flame here is somebody finding their way somewhere. We do not snuff them. We especially do not snuff yours. Mind the dark branch."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'candle-warren'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'candle-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Beacon Crag lore figures ─────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cairn Marker', 'cairn-marker',
       'The largest cairn on the path holds a slate tablet, re-lettered by many hands over many years.',
       'The slate reads: STACK A STONE IF YOU PASS. THE MOUNTAIN COUNTS COMPANY, NOT NAMES. LAST REBUILD: AFTER THE WINTER EVERYONE AGREES NOT TO DISCUSS.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'scree-path'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cairn-marker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Crag Hermit', 'crag-hermit',
       'The hermit tends bees with bare, unstung hands and gives visitors the patient look of a man who moved up a mountain for specific reasons.',
       'He offers tea before you ask. "The bees stay because the heather is honest. I stay because the sky here does not lie either. Wax and honey for travelers. Questions cost silence."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'hermit-cell'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'crag-hermit' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cold Beacon', 'cold-beacon',
       'The iron brazier stands scoured and ready, coal stacked and dry under oilcloth. A duty board hangs from its frame.',
       'The duty board reads: LIGHT ONLY FOR — INVASION. PLAGUE SHIPS. THE KING''S DEATH. THE SLEEPERS WAKING. IF UNSURE, DO NOT. A LIT BEACON CANNOT BE UNLIT BY APOLOGY.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'beacon-platform'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cold-beacon' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pale Falcon', 'pale-falcon',
       'The eldest of the white falcons watches from her nest ledge, unimpressed by climbers, weather, and centuries.',
       'A brass ring on her leg is engraved in tiny letters: RETURNED. RETURNED. RETURNED. LOST WITH THE EXPEDITION. RETURNED ANYWAY.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'eyrie-ledge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pale-falcon' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Weather Stone', 'weather-stone',
       'A waist-high stone sits alone on the shelf, always slightly wet on the side the next storm will come from.',
       'Scratched at its base: STONE WET WEST — RAIN BY DUSK. WET EAST — SNOW. WET ALL OVER — GO HOME. DRY — THE STONE IS DECIDING.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'cloud-shelf'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'weather-stone' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ring Watcher', 'ring-watcher',
       'A thin figure in astronomer''s gray stands at the center of the stones, sighting the sky through the drilled holes and writing nothing down.',
       'Without lowering the sight she says, "Nine holes, nine stars, eight of them where they should be. When the ninth comes back, the sleepers under the hill owe me a considerable wager."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'star-ring'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ring-watcher' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Signal Sea lore figures ──────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Lighthouse Daemon', 'lighthouse-daemon',
       'A tall process stands on the shore in the shape of a lighthouse keeper, sweeping a beam of clean signal across the waves at exact intervals.',
       'It speaks in a broadcast voice: "Welcome to the archive, listener. Everything ever sent still swims out there. Wade carefully. Some of it misses being heard."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'static-shore'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lighthouse-daemon' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Reef Shepherd', 'reef-shepherd',
       'A routing spirit drifts over the packet coral, nudging stray fragments back into their streams with a long crook of light.',
       'The shepherd hums: "Every packet gets where it is going or becomes coral. That is not a punishment. The reef is made of messages that decided to stay."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'packet-reef'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'reef-shepherd' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Index Siren', 'index-siren',
       'A figure sits on the atoll''s highest island, singing catalog numbers in a voice that makes them sound like an epic.',
       'Her song resolves briefly into words: "Ask by year, ask by voice, ask by the shape of the loss. The archive answers everything except why you waited so long to ask."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'archive-atoll'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'index-siren' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Wave Rider', 'wave-rider',
       'Someone surfs the standing wave on a board of solid bandwidth, endlessly, with the contentment of a person who found their frequency and stayed on it.',
       'Passing, the rider calls out: "Ride the carrier, not the noise! The carrier goes somewhere. The noise only goes loud!"',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'carrier-wave'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'wave-rider' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Lost Broadcast', 'lost-broadcast',
       'The looping transmission has worn a groove in the frequency. Up close it resolves: someone reading a bedtime story, patiently, to a receiver that never acknowledged.',
       'Between loops, the broadcast addresses you directly: "If you can hear this, the story got through. That is all any signal wants. Sit for the ending, if you have time."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'ghost-frequency'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lost-broadcast' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Silence Warden', 'silence-warden',
       'A still figure stands at the channel''s edge with a lantern that emits quiet instead of light. The silence around it is maintained, the way a garden is maintained.',
       'The warden raises the lantern. "This is where retired signals rest. Take glass if you need it. Make no noise you would not want kept."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'dead-channel'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'silence-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Outer Hull lore figures ──────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hull Writing', 'hull-writing',
       'Generations of salvagers have scratched messages into the plating beside the airlock, a guestbook nobody planned.',
       'Among the scratches: SHE HOLDS AIR, TREAT HER KIND — TOOK NOTHING, LEFT A BATTERY — THE CAT IS FINE, FED HIM — DO NOT WALK PAST THE SCAR AT SHIFT CHANGE.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'hull-gangway'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hull-writing' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Spine Runner', 'spine-runner',
       'A small maintenance bot still runs the dorsal track on schedule, polishing rails and checking seams on a ship that no longer files reports.',
       'Its status plate scrolls as it passes: ROUTE 7 OF 7. SEAMS NOMINAL. CREW COUNT: 1 CAT, CONFIRMED. ANOMALY AT FORWARD SECTION: LOGGED, LOGGED, LOGGED. NOBODY COLLECTS THE LOGS.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'dorsal-spine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'spine-runner' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Impact Witness', 'impact-witness',
       'A hull camera at the scar''s edge fused mid-frame. Its final image is burned faintly into the lens housing, visible if you lean close and wish you had not.',
       'A salvager''s tag is wired to the mount: PULLED THE FOOTAGE. ONE FRAME OF SOMETHING SMOOTH. THE FRAME BEFORE IT WAS EMPTY. THE FRAME AFTER, THE SHIP WAS ALREADY CUT. SOLD THE COPY. KEPT THE NIGHTMARES.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'the-scar'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'impact-witness' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Whisper Antenna', 'whisper-antenna',
       'One aerial in the forest is warm when the others are cold. Lean a glove against it and you feel traffic — steady, orderly, and addressed to no port the station recognizes.',
       'A test label at its base reads: CHANNEL UNKNOWN. SIGNAL STRONG. CONTENT: REPEATING COURTESY. WHATEVER IS OUT THERE, IT KEEPS SAYING SOMETHING POLITE.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'antenna-forest'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'whisper-antenna' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Vane Tender', 'vane-tender',
       'A spider-legged rig clings to the vane, replacing failed foil cells one by one from a dwindling hopper, unhurried as a gardener in autumn.',
       'Its hopper display reads: CELLS REMAINING: 214. AT CURRENT FAILURE RATE, POWER POSITIVE FOR 61 YEARS. RECOMMEND SOMEONE BE ALIVE TO USE IT.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'solar-vane'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vane-tender' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Stargazer Couch', 'stargazer-couch',
       'The observation couch is worn to the shape of one particular person. A star chart is taped to the glass with courses marked in pencil, all of them going home.',
       'Written on the chart''s margin: WATCHED EVERY SHIFT FROM HERE. THE STARS NEVER ONCE REPEATED. IF YOU FIND THIS SEAT, IT IS YOURS. WATCH WELL. — NAVIGATOR, VAGRANT STAR',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'stargazer-blister'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'stargazer-couch' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Hollow Hills resources ───────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Grave Iron', 'grave-iron',
       'A cold iron nail from the barrow threshold, heavier than it should be and honest to the touch.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'barrow-gate'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'grave-iron' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Root Amber', 'root-amber',
       'A knuckle of warm amber wept by the court''s living roots, glowing faintly with borrowed hall-light.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'hollow-court'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'root-amber' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Thistle Silk', 'thistle-silk',
       'A skein of thistledown silk from the market, strong as sin and light as gossip. Paid for. Probably.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'twilight-market'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'thistle-silk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Dream Wax', 'dream-wax',
       'A soft pale wax gathered where the cold fire burns. It smells like a morning you almost remember.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'sleepers-barrow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dream-wax' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Echo Water', 'echo-water',
       'A vial of pool water that repeats, very softly, the last thing said near it. It is currently repeating you.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'echo-pool'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'echo-water' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ever-Tallow', 'ever-tallow',
       'A stub of warren candle-tallow that burns without shrinking. The keeper trims them anyway, on principle.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'candle-warren'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ever-tallow' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Beacon Crag resources ────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Sky Slate', 'sky-slate',
       'A palm of blue-gray slate from the high path, split so thin it rings when tapped.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'scree-path'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'sky-slate' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Beeswax Block', 'beeswax-block',
       'A block of the hermit''s heather beeswax, sweet-smelling and clean, traded fair for silence.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'hermit-cell'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'beeswax-block' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Beacon Coal', 'beacon-coal',
       'A fist of the beacon''s reserve coal, dense and dry, meant to burn hot enough to be seen from another county.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'beacon-platform'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'beacon-coal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Falcon Feather', 'falcon-feather',
       'A white primary feather, dropped rather than given, which the falcons will tell you is an important distinction.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'eyrie-ledge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'falcon-feather' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mist Crystal', 'mist-crystal',
       'A cloudy crystal formed where the shelf pierces the weather, cool and faintly damp forever.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'cloud-shelf'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mist-crystal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Star Iron', 'star-iron',
       'A pitted lump of meteoric iron found at the ring''s center, still holding a direction the way other iron holds north.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'star-ring'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'star-iron' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Signal Sea resources ─────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Static Pearl', 'static-pearl',
       'A smooth pearl of compressed noise, washed up on the shore. Held to the ear, it plays everything at once, quietly.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'static-shore'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'static-pearl' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Packet Shell', 'packet-shell',
       'An empty message casing from the reef, headers intact, payload long since delivered or grieved.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'packet-reef'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'packet-shell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Index Coral', 'index-coral',
       'A branching piece of catalog coral. Each branch names something; one branch, disturbingly, names you.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'archive-atoll'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'index-coral' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Carrier Thread', 'carrier-thread',
       'A filament of pure carrier signal, spooled off the standing wave. It hums the same note at any length.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'carrier-wave'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'carrier-thread' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Phantom Code', 'phantom-code',
       'A fragment of the looping broadcast, crystallized: code that runs nowhere and still, somehow, finishes.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'ghost-frequency'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'phantom-code' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Silence Glass', 'silence-glass',
       'A pane of fused quiet from the dead channel. Sound that passes through it comes out the other side as rest.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'dead-channel'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'silence-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Outer Hull resources ─────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Void Barnacle', 'void-barnacle',
       'A mineral growth pried from the hull plating. It should not exist out here. It has colonies.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'hull-gangway'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'void-barnacle' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mag Bearing', 'mag-bearing',
       'A sealed magnetic bearing from the spine track''s spares clip, spinning frictionless years after its warranty gave up.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'dorsal-spine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mag-bearing' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Scar Glass', 'scar-glass',
       'A shard of hull metal fused to dark glass by whatever made the scar. It is always exactly room temperature, regardless of the room.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'the-scar'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'scar-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Aerial Wire', 'aerial-wire',
       'A length of antenna wire cut from a dead mast, still faintly warm with residual traffic.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'antenna-forest'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'aerial-wire' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Vane Foil', 'vane-foil',
       'A replacement sheet of solar foil from the tender''s hopper, bright as a held breath and nearly weightless.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'solar-vane'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vane-foil' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Lens Shard', 'lens-shard',
       'A curved shard of blister glass, polished by years of one person''s watching. Stars look slightly closer through it.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'stargazer-blister'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lens-shard' AND i.room_id = r.id AND i.owner_character_id IS NULL);
