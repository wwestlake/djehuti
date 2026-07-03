INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Orchard Walk', 'orchard-walk',
       'Low stone walls guide a tidy lane of espaliered fruit trees and weather-smoothed baskets waiting for the next honest load.',
       9, 4, -4
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Falcon Roost', 'falcon-roost',
       'A wind-bright platform of posts, bells, and leather perches where messenger birds once learned the shape of the whole valley.',
       10, 4, -6
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Barrow Approach', 'barrow-approach',
       'The road narrows between old marker stones and rooted lantern hooks. The air feels cooler here, as if memory itself cast shade.',
       11, 0, -6
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Weather Stone', 'weather-stone',
       'A broad standing stone rises from the heath, wrapped in prayer ribbons and scratched forecasts. Moss grips its base like a patient audience.',
       12, -2, -6
FROM mud_zones z
WHERE z.slug = 'central-hub'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Solar Gallery', 'solar-gallery',
       'Sheets of reflective foil turn slow overhead, making the gallery pulse with a patient gold light that follows the station''s roll.',
       11, 6, 6
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Packet Garden', 'packet-garden',
       'Signal pods bloom from trellised conduit here, each one storing an old message or a future one waiting to happen.',
       12, 6, 4
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Clamp Yard', 'clamp-yard',
       'Rows of magnetic braces and freight hooks line a stripped work court where salvage crews practiced impossible lifts until they got them right.',
       13, -6, 2
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Quiet Lock', 'quiet-lock',
       'A pressure lock with dim panels and thick hush seals. Even the alarms here seem trained not to raise their voices.',
       14, -6, 4
FROM mud_zones z
WHERE z.slug = 'star-reach'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the orchard walk', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'orchard-walk'
WHERE r1.slug = 'pilgrims-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the pilgrims yard', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'pilgrims-yard'
WHERE r1.slug = 'orchard-walk'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the falcon roost', 'stairs-up'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'falcon-roost'
WHERE r1.slug = 'orchard-walk'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the orchard walk', 'stairs-down'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'orchard-walk'
WHERE r1.slug = 'falcon-roost'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Toward the barrow road', 'road'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'barrow-approach'
WHERE r1.slug = 'market-crossing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the market crossing', 'road'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
WHERE r1.slug = 'barrow-approach'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Across the heath to the weather stone', 'trail'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'weather-stone'
WHERE r1.slug = 'barrow-approach'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the barrow approach', 'trail'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'barrow-approach'
WHERE r1.slug = 'weather-stone'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Out along the solar gallery', 'catwalk'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'solar-gallery'
WHERE r1.slug = 'observation-rim'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the observation rim', 'catwalk'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'observation-rim'
WHERE r1.slug = 'solar-gallery'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the packet garden', 'ladder'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'packet-garden'
WHERE r1.slug = 'solar-gallery'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the solar gallery', 'ladder'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'solar-gallery'
WHERE r1.slug = 'packet-garden'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Into the clamp yard', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'clamp-yard'
WHERE r1.slug = 'salvage-bay'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the salvage bay', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'salvage-bay'
WHERE r1.slug = 'clamp-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Toward the quiet lock', 'pressure-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'quiet-lock'
WHERE r1.slug = 'clamp-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to the clamp yard', 'pressure-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'clamp-yard'
WHERE r1.slug = 'quiet-lock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Orchard Keeper', 'orchard-keeper',
       'A patient keeper checks wicker ladders, bruised fruit, and the honest work of branches that still trust the wall.',
       'The keeper smiles without hurry. "Good fruit, good water, good roads. Fix those three and most kingdoms stop trying to die."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'orchard-walk'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'orchard-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Orchard Apple', 'orchard-apple',
       'A hard-skinned red apple packed for travel instead of ceremony.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'orchard-walk'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'orchard-apple' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Roost Falconer', 'roost-falconer',
       'Leather-gloved and wind-burned, the falconer still watches the valley as if the next message matters more than sleep.',
       'The falconer taps a perch and says, "A bird returns for three reasons: training, hunger, or love. It is best if your messages deserve all three."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'falcon-roost'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'roost-falconer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Falcon Feather', 'falcon-feather',
       'A clean dropped primary from a courier hawk, glossy and strong enough for another errand.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'falcon-roost'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'falcon-feather' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Barrow Porter', 'barrow-porter',
       'A porter with a shuttered lantern waits where the road turns solemn, ready to escort courage farther than sense would usually take it.',
       'The porter lowers his voice. "The barrow is not angry. It is careful. Carry a good light and a better intention."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'barrow-approach'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'barrow-porter' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Dream Wax', 'dream-wax',
       'A pale thumb of barrow wax that warms slowly and keeps a steady, thoughtful flame.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'barrow-approach'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dream-wax' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Weather Stone Reader', 'weather-stone-reader',
       'An old reader rubs lichen from the standing stone and listens for weather the way other people listen for gossip.',
       'The reader traces a cracked groove in the stone. "Mist first, wind second. Storms only shout after they have already written themselves down."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'weather-stone'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'weather-stone-reader' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mist Crystal', 'mist-crystal',
       'A milk-pale crystal left dewy by the weather stone, cool even under open sky.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'weather-stone'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mist-crystal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Solar Curator', 'solar-curator',
       'A soft-voiced curator keeps the foil vanes aligned so the gallery can continue pretending it is a sunrise machine.',
       'The curator does not look away from the turning mirrors. "Every station deserves one room that remembers how to make light on purpose."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'solar-gallery'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'solar-curator' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Vane Foil', 'vane-foil',
       'A scored strip of bright solar foil cut from a tuning vane and rolled for reuse.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'solar-gallery'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vane-foil' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Packet Gardener', 'packet-gardener',
       'A maintenance botanist coaxes signal pods into bloom with patient taps and a surgeon''s respect for timing.',
       'The gardener lifts a glowing pod and grins. "Messages are seeds. Most fail. The ones that matter grow roots in people anyway."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'packet-garden'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'packet-gardener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Packet Shell', 'packet-shell',
       'An empty message pod shell, still warm around the seal where its last delivery broke open.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'packet-garden'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'packet-shell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Clamp Master', 'clamp-master',
       'A broad mechanic in a brace harness checks magnetic hooks with the calm of someone who has already survived the worst possible lift.',
       'The clamp master thumps a rail for emphasis. "If it slips, you guessed. If it holds, you measured. Be the second kind of worker."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'clamp-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'clamp-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Mag Bearing', 'mag-bearing',
       'A palm-sized magnetic bearing that turns with a near-silent confidence.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'clamp-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mag-bearing' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Quiet Lock Warden', 'quiet-lock-warden',
       'A lock warden in dark maintenance cloth checks the hush seals and seems personally offended by unnecessary noise.',
       'The warden puts one finger to the visor. "Silence is not emptiness. It is spare capacity. Use it before panic spends it for you."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'quiet-lock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'quiet-lock-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Silence Glass', 'silence-glass',
       'A dark pane of pressure-tempered hush glass that swallows glare and softens nearby vibration.',
       NULL,
       true, 1
FROM mud_rooms r
WHERE r.slug = 'quiet-lock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'silence-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

GRANT ALL ON TABLE mud_rooms TO djehuti;
GRANT ALL ON TABLE mud_exits TO djehuti;
GRANT ALL ON TABLE mud_items TO djehuti;
