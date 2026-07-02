-- MUD world expansion 2: Greenwood (Medieval forest) and Vagrant Star
-- (Sci-Fi derelict ship) zones, lore figures, and crafting resources.

-- ── Zones ────────────────────────────────────────────────────────────────────

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Greenwood', 'greenwood', 'The old forest beyond the tannery gate, where trails, shrines, and working camps thread between trees older than the keep.', 6)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO mud_zones (name, slug, description, position)
VALUES ('Vagrant Star', 'vagrant-star', 'A derelict freighter clamped to the drift ring, dark and holding pressure, its last crew accounted for by no manifest anyone can find.', 7)
ON CONFLICT (slug) DO NOTHING;

-- ── Greenwood rooms ──────────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Greenwood Edge', 'greenwood-edge',
       'The trees begin abruptly at the tannery gate, a green wall of oak and alder threaded by one honest path. Sound changes here; the keep stops mattering.',
       0, 0, 0
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hunter''s Trail', 'hunters-trail',
       'A narrow trail follows deer sign between blackthorn stands. Blaze marks on the trunks are recent, cut by someone who wanted to be followed carefully or not at all.',
       1, -2, 0
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Charcoal Camp', 'charcoal-camp',
       'A ring of earth-covered mounds smolders in a cleared hollow. The burners'' lean-to stands empty, but the fires are banked with care, tended within the day.',
       2, -2, 2
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Shrine Clearing', 'shrine-clearing',
       'The trail opens on a mossy clearing where a squat stone figure stands hip-deep in ferns. Offerings rest at its feet: a bent knife, dried flowers, a child''s wooden horse.',
       3, -4, 0
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'River Ford', 'river-ford',
       'A cold river runs shallow over gravel banks. Stepping stones cross to the far side, and clay churns pale where the current cuts under the near bank.',
       4, 0, 2
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Watch Oak', 'watch-oak',
       'One great oak rises above the canopy with climbing pegs hammered up its trunk. From the fork, a watcher can see the keep, the road, and weather coming in.',
       5, 0, -2
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Vagrant Star rooms ───────────────────────────────────────────────────────

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Umbilical Gantry', 'umbilical-gantry',
       'A flexible boarding tube runs from the ring''s docking clamp to the wreck''s hull. The gantry sways slightly with the station''s slow breathing.',
       0, 0, 0
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Wreck Airlock', 'wreck-airlock',
       'The Vagrant Star''s airlock cycled its last crew through long ago and never sealed properly again. Suit racks stand empty except for one helmet, set down facing the door.',
       1, 2, 0
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Shattered Bridge', 'shattered-bridge',
       'The bridge glass is crazed white from an old impact. Every console is dark except the navigation plinth, which still holds its final course like a held breath.',
       2, 2, -2
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Crew Berths', 'crew-berths',
       'Stacked bunks line a compartment that still smells faintly of coffee and machine soap. Personal effects wait in webbing, packed for a shore leave that never came.',
       3, 4, 0
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Engine Crypt', 'engine-crypt',
       'The drive chamber is cold and cathedral-quiet. The reactor sarcophagus is intact, and dormant power cells sit in their charging racks like offerings no one collected.',
       4, 4, 2
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Cargo Hollow', 'cargo-hollow',
       'The main hold has been picked over, but its corners are a warren of crates, netting, and nest-like spaces where something small has been living comfortably.',
       5, 2, 2
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

-- ── Greenwood exits ──────────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Through the postern gate into the wood', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'greenwood-edge'
WHERE r1.slug = 'tannery-lane'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back through the postern gate', 'gate'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'tannery-lane'
WHERE r1.slug = 'greenwood-edge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Along the hunter''s trail', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'hunters-trail'
WHERE r1.slug = 'greenwood-edge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back toward the wood''s edge', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'greenwood-edge'
WHERE r1.slug = 'hunters-trail'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down into the charcoal hollow', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'charcoal-camp'
WHERE r1.slug = 'hunters-trail'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up out of the hollow', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'hunters-trail'
WHERE r1.slug = 'charcoal-camp'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Toward the shrine clearing', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'shrine-clearing'
WHERE r1.slug = 'hunters-trail'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the trail', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'hunters-trail'
WHERE r1.slug = 'shrine-clearing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the river ford', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'river-ford'
WHERE r1.slug = 'greenwood-edge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up from the riverbank', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'greenwood-edge'
WHERE r1.slug = 'river-ford'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'To the watch oak', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'watch-oak'
WHERE r1.slug = 'greenwood-edge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back toward the wood''s edge', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'greenwood-edge'
WHERE r1.slug = 'watch-oak'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Vagrant Star exits ───────────────────────────────────────────────────────

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Docking clamp to the Vagrant Star', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'umbilical-gantry'
WHERE r1.slug = 'cargo-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back through the clamp to the cargo spine', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cargo-spine'
WHERE r1.slug = 'umbilical-gantry'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Through the wreck''s airlock', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'wreck-airlock'
WHERE r1.slug = 'umbilical-gantry'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the gantry', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'umbilical-gantry'
WHERE r1.slug = 'wreck-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Forward to the shattered bridge', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'shattered-bridge'
WHERE r1.slug = 'wreck-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Aft to the airlock', 'sealed-door'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'wreck-airlock'
WHERE r1.slug = 'shattered-bridge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Down the companionway to the berths', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'crew-berths'
WHERE r1.slug = 'wreck-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back up the companionway', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'wreck-airlock'
WHERE r1.slug = 'crew-berths'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down into the engine crypt', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'engine-crypt'
WHERE r1.slug = 'crew-berths'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up out of the drive chamber', 'bulkhead'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'crew-berths'
WHERE r1.slug = 'engine-crypt'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down into the cargo hollow', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'cargo-hollow'
WHERE r1.slug = 'wreck-airlock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up toward the airlock', 'passage'
FROM mud_rooms r1
JOIN mud_rooms r2 ON r2.slug = 'wreck-airlock'
WHERE r1.slug = 'cargo-hollow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

-- ── Greenwood lore figures ───────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Verge Warden', 'verge-warden',
       'A gray-cloaked warden stands where the path enters the trees, counting travelers in and, more carefully, counting them out.',
       'The warden holds up three fingers. "Wood rules: take bark, not trees. Feed no fires you cannot bank. And if the shrine asks for something, leave it."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'greenwood-edge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'verge-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Silent Hunter', 'silent-hunter',
       'A hunter kneels beside the trail reading ground sign, so still that you notice the bow before you notice the man.',
       'Without turning he murmurs, "Sinew and patience take more game than steel. The wood feeds whoever stops asking it to hurry."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'hunters-trail'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'silent-hunter' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Charcoal Burner', 'charcoal-burner',
       'A soot-blackened burner circles the smoldering mounds, reading smoke the way sailors read sky.',
       'She taps a mound with her rake. "Slow fire makes strong coal. Fast fire makes ash and regret. Same rule works on people."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'charcoal-camp'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'charcoal-burner' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Moss Idol', 'moss-idol',
       'The squat stone figure is older than the keep, its features soft under centuries of moss. The ferns around it grow in a perfect circle.',
       'Scratched into the stone base, in letters refreshed by many hands: ASK QUIETLY. TAKE LITTLE. THE WOOD REMEMBERS BOTH KINDS OF GUEST.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'shrine-clearing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'moss-idol' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ferry Post', 'ferry-post',
       'A leaning post marks the ford, hung with tallies, warnings, and one bell green with age.',
       'The tally board reads: CROSSABLE AT KNEE DEPTH. AT HIP DEPTH, WAIT. THE RIVER IS FASTER THAN YOU AND HAS NOWHERE TO BE.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'river-ford'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ferry-post' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Oak Sentinel', 'oak-sentinel',
       'High in the fork of the watch oak sits a lookout wrapped in weather-gray wool, who may have been there an hour or a season.',
       'The sentinel calls down softly, "Road is clear. Weather is coming from the west by dusk. Whatever you are doing out here, finish it dry."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'watch-oak'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oak-sentinel' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Vagrant Star lore figures ────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Clamp Warden', 'clamp-warden',
       'A docking-control fragment lives in the gantry panel, awake only enough to mind the clamp and worry about it.',
       'The panel scrolls: "Clamp integrity nominal. Vessel registry: VAGRANT STAR, ownership disputed, crew status UNRESOLVED. Boarders assume all narrative risk."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'umbilical-gantry'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'clamp-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pressure Ghost', 'pressure-ghost',
       'The airlock speaks with a voice assembled from cycling routines, polite and slightly out of date.',
       'It announces: "Welcome aboard. Atmosphere holds at ninety-one percent. The crew asks that you close what you open and finish what they could not."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'wreck-airlock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pressure-ghost' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Captain''s Log', 'captains-log',
       'The navigation plinth holds one preserved log entry under cracked glass, looping its final playback for anyone who asks.',
       'Final entry: "Course holds true. If anyone reads this — the heading was right. The ship was right. Whatever found us was simply faster. Take the chart. Finish the run."',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'shattered-bridge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'captains-log' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Bunk Shrine', 'bunk-shrine',
       'The lowest bunk has been made into a small shrine by former salvagers: photographs, a folded flag, and a cup that is always set upright.',
       'A card among the photographs reads: WE TOOK NOTHING FROM THIS ROOM. NEITHER SHOULD YOU. EVERYTHING ELSE, THE CREW WOULD WANT USED.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'crew-berths'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'bunk-shrine' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Drive Echo', 'drive-echo',
       'When the reactor sarcophagus ticks as it cools, the sound comes back wrong — a half-beat late, as if the engine room were answering itself.',
       'A maintenance slate beside the sarcophagus reads: SHE WILL START. WE CHECKED EVERYTHING TWICE. IT WAS NEVER THE ENGINE. — CHIEF ENGINEER, LAST SHIFT',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'engine-crypt'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'drive-echo' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ship''s Cat', 'ships-cat',
       'A lean gray cat regards you from a nest of netting with the unbothered authority of the wreck''s only remaining crew member.',
       'Its collar tag is engraved: BOSUN — VAGRANT STAR. IF FOUND, YOU ARE ABOARD MY SHIP. RATIONS ACCEPTED.',
       false, 0
FROM mud_rooms r
WHERE r.slug = 'cargo-hollow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ships-cat' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Greenwood resources ──────────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Willow Bark', 'willow-bark',
       'Curled strips of willow bark, bitter to the tongue and prized by healers.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'greenwood-edge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'willow-bark' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Sinew Cord', 'sinew-cord',
       'A hank of worked sinew, strong and springy, coiled hunter-fashion.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'hunters-trail'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'sinew-cord' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pitch Knot', 'pitch-knot',
       'A resin-heavy pine knot that will hold a stubborn flame in wind or rain.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'charcoal-camp'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pitch-knot' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Moon Moss', 'moon-moss',
       'A soft pad of silver-green moss from the shrine circle, cool to the touch long after picking.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'shrine-clearing'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'moon-moss' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'River Clay', 'river-clay',
       'A wrapped lump of fine pale clay cut from under the ford''s near bank.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'river-ford'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'river-clay' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Oak Gall', 'oak-gall',
       'A handful of round oak galls, the kind scribes boil down for strong dark ink.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'watch-oak'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oak-gall' AND i.room_id = r.id AND i.owner_character_id IS NULL);

-- ── Vagrant Star resources ───────────────────────────────────────────────────

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Tether Line', 'tether-line',
       'A coiled EVA tether with intact carabiners, rated for far worse than anything you plan to do.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'umbilical-gantry'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'tether-line' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Seal Ring', 'seal-ring',
       'A pliable pressure-seal ring from the airlock spares locker, still in its wrapper.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'wreck-airlock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'seal-ring' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Nav Crystal', 'nav-crystal',
       'The navigation plinth''s memory crystal, still warm, still holding the Vagrant Star''s final true course.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'shattered-bridge'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'nav-crystal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Thermal Blanket', 'thermal-blanket',
       'A folded emergency thermal blanket, silver side bright as the day it was stowed.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'crew-berths'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'thermal-blanket' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Plasma Cell', 'plasma-cell',
       'A dormant plasma cell from the charging rack, heavy with stored charge and patient about it.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'engine-crypt'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'plasma-cell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ration Tin', 'ration-tin',
       'A sealed long-life ration tin, dented but sound. The label promises stew and delivers a memory of stew.',
       NULL, true, 2
FROM mud_rooms r
WHERE r.slug = 'cargo-hollow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ration-tin' AND i.room_id = r.id AND i.owner_character_id IS NULL);
