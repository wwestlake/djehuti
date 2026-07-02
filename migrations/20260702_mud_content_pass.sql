-- MUD content expansion for both realms

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Lower Vaults', 'lower-vaults', 'Cracked stairs and buried chambers beneath the keep, where sealed relics and old workrooms remain.', 3)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Old Stair', 'old-stair',
       'A steep stone stair curls below the throne room. Damp air rises from the dark and the mortar smells of old ash.',
       0, 0, 0
FROM mud_zones z
WHERE z.slug = 'lower-vaults'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Reliquary', 'reliquary',
       'Iron cages, saint-marked boxes, and cracked plinths fill the chamber. Someone once sorted dangerous things here with religious care.',
       1, 2, 0
FROM mud_zones z
WHERE z.slug = 'lower-vaults'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Flooded Archive', 'flooded-archive',
       'Shallow black water covers the floor between fallen shelves. Rot, paper pulp, and silverfish cling to the drowned edges of the room.',
       2, -2, 0
FROM mud_zones z
WHERE z.slug = 'lower-vaults'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Forge Cellar', 'forge-cellar',
       'A cellar forge sits cold beneath a smoke-black vent. Bent tools and resin jars remain on a workbench lit by reflected coals.',
       3, 0, 2
FROM mud_zones z
WHERE z.slug = 'lower-vaults'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Warden Vault', 'warden-vault',
       'A narrow vault of reinforced stone and iron lockers. The room feels less sacred than practical, built to keep specific objects out of reach.',
       4, 4, 0
FROM mud_zones z
WHERE z.slug = 'lower-vaults'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Service Concourse', 'service-concourse',
       'A machine-lit corridor runs beneath pulsing conduits. Signs point to cryo storage, maintenance, and signal control.',
       1, 0, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Sensor Loft', 'sensor-loft',
       'A raised loft of instrument frames and glass panels. Faint data blooms slide across dead screens like weather ghosts.',
       2, 0, 4
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Cryo Gallery', 'cryo-gallery',
       'Rows of sealed pods stand in a frost-lit hall. The gallery is quiet except for the click and hiss of old pressure valves.',
       3, 2, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hydro Bay', 'hydro-bay',
       'Reservoir pipes and algae tanks crowd the bay. Coolant drips somewhere out of sight in a patient metallic rhythm.',
       4, -2, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Reactor Causeway', 'reactor-causeway',
       'A suspended causeway crosses a roaring shaft of heat and light. Warning sigils blink along the handrails in synchronized bursts.',
       5, 4, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Signal Apex', 'signal-apex',
       'At the highest control point of Star Reach, relay vanes angle toward the void. Every surface hums with distant transmission.',
       6, 4, 4
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Stone steps into the lower vaults', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-stair'
WHERE r1.slug = 'council-chamber'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Back to the throne room', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'council-chamber'
WHERE r1.slug = 'old-stair'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the reliquary', 'door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'reliquary'
WHERE r1.slug = 'old-stair'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Toward the flooded archive', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'flooded-archive'
WHERE r1.slug = 'old-stair'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Toward the forge cellar', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'forge-cellar'
WHERE r1.slug = 'old-stair'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the stair', 'door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-stair'
WHERE r1.slug = 'reliquary'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Iron steps into the warden vault', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'warden-vault'
WHERE r1.slug = 'reliquary'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the stair', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-stair'
WHERE r1.slug = 'flooded-archive'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the stair', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'old-stair'
WHERE r1.slug = 'forge-cellar'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Back to the reliquary', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'reliquary'
WHERE r1.slug = 'warden-vault'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Into the service concourse', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
WHERE r1.slug = 'transit-dock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to the dock', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'transit-dock'
WHERE r1.slug = 'service-concourse'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Lift to the sensor loft', 'elevator'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'sensor-loft'
WHERE r1.slug = 'service-concourse'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Lift back to the concourse', 'elevator'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
WHERE r1.slug = 'sensor-loft'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward cryo storage', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cryo-gallery'
WHERE r1.slug = 'service-concourse'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Toward the hydro bay', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'hydro-bay'
WHERE r1.slug = 'service-concourse'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the concourse', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
WHERE r1.slug = 'cryo-gallery'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the concourse', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
WHERE r1.slug = 'hydro-bay'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Toward the reactor causeway', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'reactor-causeway'
WHERE r1.slug = 'cryo-gallery'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to cryo storage', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cryo-gallery'
WHERE r1.slug = 'reactor-causeway'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'up', 'Climb to the signal apex', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'signal-apex'
WHERE r1.slug = 'reactor-causeway'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'down', 'Down to the causeway', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'reactor-causeway'
WHERE r1.slug = 'signal-apex'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Gate Warden', 'gate-warden',
       'An old gate warden in a patched surcoat watches the portal traffic with suspicious eyes and perfect stillness.',
       'The warden taps two fingers against a brass ledger. "Torch first. Vaults second. If something whispers your true name below, do not whisper back."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'keep-gate'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'gate-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Archivist Shade', 'archivist-shade',
       'A dim figure drifts between drowned shelves, turning phantom pages with great care.',
       'The shade murmurs, "Ink remembers what stone forgets. If you must carry one thing upward, carry the mark that leads you back."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'flooded-archive'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'archivist-shade' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Forge Servitor', 'forge-servitor',
       'A soot-dark mechanical servitor sits folded beside the cold forge, its hands still shaped for careful repair work.',
       'A cracked speaker clicks alive: "Wrap before heat. Seal before strain. Cheap work breaks twice."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'forge-cellar'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'forge-servitor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Vault Register', 'vault-register',
       'A chained register lists what was moved into and out of the lower vaults.',
       'Vault register: chalk, wax, spare nails, lamp stores, confiscated signal brass, sealed reliquary keys, and one item redacted by order of the warden.',
       false, 1
FROM mud_rooms r
WHERE r.slug = 'warden-vault'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vault-register' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Dock Clerk Drone', 'dock-clerk-drone',
       'A hovering service drone clicks through ancient docking routines as if a ship might arrive any moment.',
       'The drone projects a faded line of text: "Transit lanes unstable. Service personnel authorized to improvise bridge tools from scrap."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'transit-dock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dock-clerk-drone' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Maintenance Hologram', 'maintenance-hologram',
       'A looping technician hologram points endlessly toward marked service routes.',
       'The hologram flickers: "Coolant west. Cryo east. Sensor lift above. Do not cross the causeway without a live beacon."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'service-concourse'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'maintenance-hologram' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cryo Steward', 'cryo-steward',
       'A pod-side steward frame hangs at the edge of the gallery, its sensors still tracking temperature and seal integrity.',
       'The steward whispers through static: "Pressure holds. Route the charge before you route the heat. Nothing fragile survives both at once."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'cryo-gallery'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cryo-steward' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Array Whisper', 'array-whisper',
       'A relay ghost rides the humming vanes at the apex, present as a voice before it is visible as a shape.',
       'A whisper skates across the metal: "Signal wants structure. Noise wants panic. Build the bridge, mark the route, then listen."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'signal-apex'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'array-whisper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rune Chalk', 'rune-chalk',
       'A dry cylinder of marked chalk used to note inspected passages and warded crates.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'flooded-archive'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rune-chalk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Resin Pitch', 'resin-pitch',
       'A tin of dark resin pitch, still tacky enough to bind cloth, seal cracks, or weather rough handling.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'forge-cellar'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'resin-pitch' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Iron Nails', 'iron-nails',
       'A fistful of square-forged nails collected in a cloth pouch.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'warden-vault'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'iron-nails' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Fiber Bundle', 'fiber-bundle',
       'A coil of synthetic weave stripped from maintenance packing and still strong enough to lash gear together.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'transit-dock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fiber-bundle' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Capacitor Cell', 'capacitor-cell',
       'A thumb-length charge cell with enough reserve to jump a simple device or stabilize a rough-built circuit.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'cryo-gallery'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'capacitor-cell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Coolant Canister', 'coolant-canister',
       'A compact coolant canister chilled enough to fog in your hand when turned.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'hydro-bay'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'coolant-canister' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Crystal Vial', 'crystal-vial',
       'A clear relay-grade vial that catches and bends blue light through microcut facets.',
       NULL,
       true, 2
FROM mud_rooms r
WHERE r.slug = 'sensor-loft'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'crystal-vial' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Relay Schematic', 'relay-schematic',
       'A thin schematic sheet showing how signal paths were patched around dead relays during emergencies.',
       'Emergency relay note: when dedicated hardware fails, pair a live cell with spare wire and bridge the path by hand. Mark the route before you energize it.',
       true, 1
FROM mud_rooms r
WHERE r.slug = 'sensor-loft'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'relay-schematic' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Dock Manifest', 'dock-manifest',
       'A slate manifest of cargo once routed through Star Reach.',
       'Dock manifest: coolant, fibers, relay glass, reserve cells, and sealed cryo notices. Priority route remains the apex relay stack.',
       true, 1
FROM mud_rooms r
WHERE r.slug = 'transit-dock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dock-manifest' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- Additional realm branches, lore objects, and scavenging resources

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Chapel Landing', 'chapel-landing',
       'A broad landing of votive alcoves, travel hooks, and worn stone benches where people once paused before heading deeper into the keep.',
       5, 0, -2
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Scriptorium', 'scriptorium',
       'Desks, shelves, and scarred writing stands fill the room. Wax dust and charcoal still cling to the grain of the tables.',
       6, 2, -2
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Market Crossing', 'market-crossing',
       'A covered crossing of carts, stacked crates, and old stall frames. Even empty, the place feels built for exchange and rumor.',
       7, 0, -4
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Pilgrims Yard', 'pilgrims-yard',
       'A quiet stone yard marked by boot-scraped flags and old travel emblems pressed into the walls by generations of visitors.',
       8, 2, -4
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Mess Deck', 'mess-deck',
       'Fold-down tables, ration slots, and dented warming units line a long compartment where station workers once traded complaints and gossip.',
       7, 2, -2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Salvage Bay', 'salvage-bay',
       'Stripped machine housings and tagged scrap pallets sit under a crane track that still groans when the station shifts.',
       8, -4, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Relay Works', 'relay-works',
       'Open signal frames and maintenance gantries crowd the chamber. It smells like ozone, dust, and careful improvisation.',
       9, 2, 4
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Observation Rim', 'observation-rim',
       'A narrow rim walkway curves beneath a vaulted viewplate. The stars beyond look close enough to sort by hand.',
       10, 4, 6
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Steps toward the chapel landing', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
WHERE r1.slug = 'atrium'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the gatehouse', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'atrium'
WHERE r1.slug = 'chapel-landing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Through the writing arch', 'door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'scriptorium'
WHERE r1.slug = 'chapel-landing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the landing', 'door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
WHERE r1.slug = 'scriptorium'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Toward the market crossing', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
WHERE r1.slug = 'chapel-landing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the chapel landing', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
WHERE r1.slug = 'market-crossing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Into the pilgrims yard', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'pilgrims-yard'
WHERE r1.slug = 'market-crossing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the crossing', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
WHERE r1.slug = 'pilgrims-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the mess deck', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'mess-deck'
WHERE r1.slug = 'transit-dock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the dock', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'transit-dock'
WHERE r1.slug = 'mess-deck'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Through the salvage hatch', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'salvage-bay'
WHERE r1.slug = 'hydro-bay'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to hydro control', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'hydro-bay'
WHERE r1.slug = 'salvage-bay'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the relay works', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'relay-works'
WHERE r1.slug = 'sensor-loft'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the loft', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'sensor-loft'
WHERE r1.slug = 'relay-works'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Along the rim access', 'catwalk'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'observation-rim'
WHERE r1.slug = 'signal-apex'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to the signal apex', 'catwalk'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'signal-apex'
WHERE r1.slug = 'observation-rim'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Chapel Caretaker', 'chapel-caretaker',
       'A caretaker wrapped in weathered blue cloth tends dead votive cups and travel benches with ceremonial patience.',
       'The caretaker says, "Travelers used to leave with three things: a mark, a map, and a reason to come back."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'chapel-landing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'chapel-caretaker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ink-Stained Novice', 'ink-stained-novice',
       'A young copyist sits with charcoal on both hands, preserving forms and fragments no one else thought worth saving.',
       'Without looking up, the novice mutters, "Records are how a place keeps breathing after the people leave."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'scriptorium'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ink-stained-novice' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mule Handler', 'mule-handler',
       'A broad-shouldered handler keeps counting phantom deliveries, still making room for one more cart in his head.',
       'The handler gives you a measuring glance. "Cord, clasps, food, fire. That is what actually moves a keep, not speeches."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'market-crossing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mule-handler' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pilgrim Standard', 'pilgrim-standard',
       'A patched travel banner hangs from a low pole, stitched with names, vows, and route marks from many journeys.',
       'The standard reads: WALK FAR, CARRY LIGHT, RETURN WITH NEWS.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'pilgrims-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pilgrim-standard' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Galley Kiosk', 'galley-kiosk',
       'A ration kiosk still cycles menus to a crew that never quite arrives.',
       'Menu loop: broth, grain cakes, protein wraps, hot tea, coolant-safe water. Report shortages to deck control.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'mess-deck'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'galley-kiosk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Salvage Rig', 'salvage-rig',
       'A hulking work rig with magnetic claws waits over pallets of stripped parts and tagged machine skin.',
       'A maintenance tag dangles from the rig: TAKE ONLY WHAT YOU CAN MOUNT, PATCH, OR CARRY HOME.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'salvage-bay'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'salvage-rig' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Relay Foreman', 'relay-foreman',
       'A foreman projection paces between open signal frames, still auditing work that can no longer be officially assigned.',
       'The projection stops long enough to say, "Document the patch. If it holds, it becomes procedure."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'relay-works'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'relay-foreman' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rim Echo', 'rim-echo',
       'A voice without a visible source drifts around the rim, half memory and half instrumentation bleed.',
       'The echo whispers, "Out here, navigation is not direction. It is commitment."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'observation-rim'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rim-echo' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Charcoal Stick', 'charcoal-stick',
       'A wrapped writing charcoal, flat on one side from patient use across rough paper and wood.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'scriptorium'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'charcoal-stick' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Linen Cord', 'linen-cord',
       'A neat bundle of waxed linen cord used for tying packets, sealing rolls, or binding field gear.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'market-crossing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'linen-cord' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Copper Clasp', 'copper-clasp',
       'A small hammered clasp polished bright by repeated fastening and reuse.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'pilgrims-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'copper-clasp' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pilgrim Token', 'pilgrim-token',
       'A stamped travel token worn thin at the edges, passed from one journey to the next.',
       'One side reads ROAD. The other reads RETURN.',
       true, 1
FROM mud_rooms r
WHERE r.slug = 'chapel-landing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pilgrim-token' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Glow Filament', 'glow-filament',
       'A flexible luminous filament salvaged from old deck lighting and still bright enough to guide close work.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'mess-deck'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'glow-filament' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Alloy Plate', 'alloy-plate',
       'A hand-sized plate of station alloy cut clean from a damaged panel and stacked for reuse.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'salvage-bay'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'alloy-plate' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Data Shard', 'data-shard',
       'A fractured storage shard carrying partial route and maintenance traces.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'relay-works'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'data-shard' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Sealant Foam', 'sealant-foam',
       'A pressure tube of expanding sealant foam used to close leaks and brace stressed seams.',
       NULL,
       true, 3
FROM mud_rooms r
WHERE r.slug = 'hydro-bay'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'sealant-foam' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Void Log', 'void-log',
       'A slim observation log clipped beneath the rim glass, full of route notes and private starwatch comments.',
       'Observation log: three steady lanes remain. One for arrival, one for signal, one for going home when you are finished becoming someone else.',
       true, 1
FROM mud_rooms r
WHERE r.slug = 'observation-rim'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'void-log' AND i.room_id = r.id AND i.owner_character_id IS NULL);
