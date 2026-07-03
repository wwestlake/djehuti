INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Stable Yard', 'stable-yard',
       'A broad yard of hitching posts, feed bins, and rain-smoothed stones where the keep keeps its practical promises.',
       13, 2, 0
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Farrier Shed', 'farrier-shed',
       'Heat, hoof smoke, and the ring of hammer on shoe crowd this narrow shed built for work that cannot wait for daylight.',
       14, 4, 0
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Watch Cistern', 'watch-cistern',
       'A stone cistern under a grate of old iron collects cold ward water and the gossip of every roofline above it.',
       15, 0, 4
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Clothier Row', 'clothier-row',
       'Dyed banners, patched cloaks, and practical needlework hang from beams in a lane that smells of soap and stubborn trade.',
       16, -4, 2
FROM mud_zones z
WHERE z.slug = 'outer-ward'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Fern Hollow', 'fern-hollow',
       'A cool green dip in the wood where broad ferns collect dew and even hurried footsteps feel politely unwelcome.',
       7, 2, 2
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Moonrun Bank', 'moonrun-bank',
       'A silver-banked stream bend where reeds glow faintly at dusk and every ripple looks like it knows a route home.',
       8, 2, 4
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Briar Post', 'briar-post',
       'An old forester''s marker post leans in a tangle of blackthorn and red string, warning strangers with admirable clarity.',
       9, -6, 0
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Owl Stand', 'owl-stand',
       'A laddered hunting perch rises above the canopy here, trimmed with molted feathers and patient silence.',
       10, 0, -4
FROM mud_zones z
WHERE z.slug = 'greenwood'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Grave Meadow', 'grave-meadow',
       'A quiet meadow dotted with low stones and pale flowers where the hill buries memory without burying names.',
       7, 2, 0
FROM mud_zones z
WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Lantern Rill', 'lantern-rill',
       'A thin runnel threads the hill with floating lantern cups, each carrying a small light for someone not yet forgotten.',
       8, 2, -4
FROM mud_zones z
WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Oath Circle', 'oath-circle',
       'Flat standing stones ring a patch of clipped grass where promises are traded, witnessed, and very rarely broken twice.',
       9, 2, 2
FROM mud_zones z
WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hush Fen', 'hush-fen',
       'The ground softens into black water and whisper reeds. The fen does not demand quiet; it simply makes loudness feel foolish.',
       10, -6, 0
FROM mud_zones z
WHERE z.slug = 'hollow-hills'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Recycler Nest', 'recycler-nest',
       'Crates of stripped plating and sorted salvage crowd this little work pocket where nothing stays junk if someone still needs it.',
       7, 4, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Mag-Rail Pier', 'mag-rail-pier',
       'A narrow service pier runs beside an idle magnetic rail, humming with stored momentum and old departure schedules.',
       8, 6, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Pulse Orchard', 'pulse-orchard',
       'Bioelectric fruit nodes hang in ordered rows from insulated branches, blinking softly like disciplined stars.',
       9, -4, 0
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Quarantine Lock', 'quarantine-lock',
       'A bright-sealed decon lock with strip lights, warning placards, and the clean tension of procedures that matter.',
       10, -2, 4
FROM mud_zones z
WHERE z.slug = 'drift-ring'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Foam Cache', 'foam-cache',
       'Pressure crates float in a pocket of signal surf here, cushioned by amber foam that never quite collapses.',
       7, 6, 0
FROM mud_zones z
WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Tide Switch', 'tide-switch',
       'A reef of switching vanes and timing fins redirects packet tides through the sea with mechanical grace.',
       8, 6, -2
FROM mud_zones z
WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Hush Current', 'hush-current',
       'A slow channel of muted signal drifts past, carrying old voices in tones too soft to become demands.',
       9, -4, 0
FROM mud_zones z
WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Beacon Kelp', 'beacon-kelp',
       'Tall strands of luminous kelp sway around a ruined beacon mast, each frond flashing directional code in the dark.',
       10, 0, -4
FROM mud_zones z
WHERE z.slug = 'signal-sea'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Captain Shrine', 'captain-shrine',
       'A wall niche of snapped insignia, candle clips, and one intact command bolt honors the bridge crew who stayed too long.',
       7, 4, -2
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Cryo Choir', 'cryo-choir',
       'Ranks of cold tubes stand open here, and every shifting draft pulls a glass note from the frost-lined housings.',
       8, 6, 0
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Ballast Maw', 'ballast-maw',
       'A deep cargo throat yawns below a rack of dead grav-hooks, swallowing light and throwing back only practical echoes.',
       9, 2, 4
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
SELECT z.id, 'Ash Drive', 'ash-drive',
       'The auxiliary drive tunnel is choked with soot and spent carbon fins, but something here still remembers ignition.',
       10, 6, 2
FROM mud_zones z
WHERE z.slug = 'vagrant-star'
ON CONFLICT (zone_id, slug) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the stable yard', 'gate'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'stable-yard'
WHERE r1.slug = 'bailey-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the bailey yard', 'gate'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
WHERE r1.slug = 'stable-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the farrier shed', 'door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'farrier-shed'
WHERE r1.slug = 'stable-yard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the stable yard', 'door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'stable-yard'
WHERE r1.slug = 'farrier-shed'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the watch cistern', 'stairs-down'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'watch-cistern'
WHERE r1.slug = 'old-well'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the old well', 'stairs-up'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'old-well'
WHERE r1.slug = 'watch-cistern'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Toward clothier row', 'lane'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'clothier-row'
WHERE r1.slug = 'tannery-lane'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to tannery lane', 'lane'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'tannery-lane'
WHERE r1.slug = 'clothier-row'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Along the fern hollow', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'fern-hollow'
WHERE r1.slug = 'river-ford'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the river ford', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'river-ford'
WHERE r1.slug = 'fern-hollow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the moonrun bank', 'ford'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'moonrun-bank'
WHERE r1.slug = 'fern-hollow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to fern hollow', 'ford'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'fern-hollow'
WHERE r1.slug = 'moonrun-bank'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Toward the briar post', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'briar-post'
WHERE r1.slug = 'shrine-clearing'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the shrine clearing', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'shrine-clearing'
WHERE r1.slug = 'briar-post'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the owl stand', 'ladder'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'owl-stand'
WHERE r1.slug = 'watch-oak'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the watch oak', 'ladder'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'watch-oak'
WHERE r1.slug = 'owl-stand'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Into the grave meadow', 'path'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'grave-meadow'
WHERE r1.slug = 'barrow-gate'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the barrow gate', 'path'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
WHERE r1.slug = 'grave-meadow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the lantern rill', 'path'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'lantern-rill'
WHERE r1.slug = 'twilight-market'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the twilight market', 'path'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'twilight-market'
WHERE r1.slug = 'lantern-rill'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'To the oath circle', 'ring'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'oath-circle'
WHERE r1.slug = 'sleepers-barrow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the sleepers'' barrow', 'ring'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'sleepers-barrow'
WHERE r1.slug = 'oath-circle'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Into the hush fen', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hush-fen'
WHERE r1.slug = 'echo-pool'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to the echo pool', 'trail'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'echo-pool'
WHERE r1.slug = 'hush-fen'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the recycler nest', 'bulkhead'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'recycler-nest'
WHERE r1.slug = 'cargo-spine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to cargo spine', 'bulkhead'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cargo-spine'
WHERE r1.slug = 'recycler-nest'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Out to the mag-rail pier', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'mag-rail-pier'
WHERE r1.slug = 'recycler-nest'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the recycler nest', 'catwalk'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'recycler-nest'
WHERE r1.slug = 'mag-rail-pier'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Into the pulse orchard', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'pulse-orchard'
WHERE r1.slug = 'botany-loop'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to botany loop', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'botany-loop'
WHERE r1.slug = 'pulse-orchard'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down to the quarantine lock', 'pressure-door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'quarantine-lock'
WHERE r1.slug = 'medbay-annex'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to medbay annex', 'pressure-door'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'medbay-annex'
WHERE r1.slug = 'quarantine-lock'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the foam cache', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'foam-cache'
WHERE r1.slug = 'archive-atoll'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to archive atoll', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'archive-atoll'
WHERE r1.slug = 'foam-cache'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up to the tide switch', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'tide-switch'
WHERE r1.slug = 'foam-cache'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to the foam cache', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'foam-cache'
WHERE r1.slug = 'tide-switch'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Into the hush current', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hush-current'
WHERE r1.slug = 'dead-channel'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Back to dead channel', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'dead-channel'
WHERE r1.slug = 'hush-current'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Up through the beacon kelp', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'beacon-kelp'
WHERE r1.slug = 'carrier-wave'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Back to carrier wave', 'current'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'carrier-wave'
WHERE r1.slug = 'beacon-kelp'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the captain shrine', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'captain-shrine'
WHERE r1.slug = 'shattered-bridge'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the shattered bridge', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'shattered-bridge'
WHERE r1.slug = 'captain-shrine'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the cryo choir', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cryo-choir'
WHERE r1.slug = 'crew-berths'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to crew berths', 'hatch'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'crew-berths'
WHERE r1.slug = 'cryo-choir'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'south', 'Down into the ballast maw', 'ladder'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'ballast-maw'
WHERE r1.slug = 'cargo-hollow'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'north', 'Back to cargo hollow', 'ladder'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cargo-hollow'
WHERE r1.slug = 'ballast-maw'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'east', 'Toward the ash drive', 'tunnel'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'ash-drive'
WHERE r1.slug = 'engine-crypt'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
SELECT r1.id, r2.id, 'west', 'Back to the engine crypt', 'tunnel'
FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'engine-crypt'
WHERE r1.slug = 'ash-drive'
ON CONFLICT (from_room_id, direction) DO NOTHING;

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Stable Master', 'stable-master',
       'A broad-shouldered stable master checks tack, feed, and the character of anyone touching the horses.',
       'The stable master shrugs. "Good walls matter. Good hooves matter more if you ever plan to leave them."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'stable-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'stable-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Stable Nails', 'stable-nails',
       'A small wrapped bundle of square stable nails, blackened against rust and honest use.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'stable-yard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'stable-nails' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Farrier', 'farrier',
       'The farrier works by heat memory and blunt patience, speaking to iron as if it has earned the courtesy.',
       'Without looking up, the farrier mutters, "Most trouble starts when people ignore what carries them."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'farrier-shed'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'farrier' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hoof Iron', 'hoof-iron',
       'A curved offcut of worked shoe iron, still warm with the memory of the hammer.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'farrier-shed'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hoof-iron' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cistern Tender', 'cistern-tender',
       'A hooded tender keeps the ward water clear, counting every bucket like it is a vote for tomorrow.',
       'The tender taps the stone lip. "Water is what a wall drinks when fear burns too hot."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'watch-cistern'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cistern-tender' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cistern Salt', 'cistern-salt',
       'A twist of mineral ward salt scraped from the cool stones above the waterline.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'watch-cistern'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cistern-salt' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cloth Factor', 'cloth-factor',
       'A cloth merchant with sharp eyes and soft hands appraises every stitch the way generals inspect walls.',
       'The factor smiles thinly. "Fashion is just logistics with better posture."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'clothier-row'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cloth-factor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Spool Silk', 'spool-silk',
       'A hard-wound spool of trade silk, strong enough to earn the price tied to it.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'clothier-row'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'spool-silk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Fern Keeper', 'fern-keeper',
       'A quiet greenwood keeper kneels among the fronds, reading bent stalks the way courtiers read moods.',
       'The keeper brushes dew from a frond. "If the forest wanted speed, it would have paved itself."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'fern-hollow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fern-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Fern Frond', 'fern-frond',
       'A broad medicinal frond bundled wet against a strip of bark.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'fern-hollow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fern-frond' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Reed Gatherer', 'reed-gatherer',
       'Barefoot and river-calm, the gatherer cuts moon reeds at the root so the bank keeps its shape.',
       'The gatherer nods toward the stream. "Take only what still lets the water remember itself."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'moonrun-bank'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'reed-gatherer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Moon Reed', 'moon-reed',
       'A pale hollow reed that rings softly when tapped against the knee.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'moonrun-bank'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'moon-reed' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Thorn Warden', 'thorn-warden',
       'A warden in scarred gloves tends the warning cords and respects the briars more than most people.',
       'The warden snorts. "Thorns are just fences that grew tired of asking nicely."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'briar-post'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'thorn-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Briar Thorn', 'briar-thorn',
       'A dark hooked thorn clipped clean from the warning wall.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'briar-post'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'briar-thorn' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Owl Keeper', 'owl-keeper',
       'A patient keeper with a leather cuff and moon-pale eyes stands still enough for the birds to trust the arrangement.',
       'The keeper smiles into the dark. "Owls are not silent. They are simply too competent to boast."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'owl-stand'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'owl-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Owl Pellet', 'owl-pellet',
       'A dry little pellet wrapped in feather fluff and woodland fact.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'owl-stand'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'owl-pellet' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Meadow Sexton', 'meadow-sexton',
       'The sexton tends the low stones with a gardener''s care and a diplomat''s respect for old grievances.',
       'The sexton rests on the spade. "The dead rarely ask for much. Usually just accuracy."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'grave-meadow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'meadow-sexton' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Grave Bloom', 'grave-bloom',
       'A pale hillflower cut with both apology and permission.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'grave-meadow'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'grave-bloom' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rill Tender', 'rill-tender',
       'Lantern cups drift around the tender''s boots while they sort wicks and names with practiced mercy.',
       'The tender watches a light spin past. "Every lantern is a message. Most of them are just: still here."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'lantern-rill'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rill-tender' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rill Glass', 'rill-glass',
       'A water-smoothed bead of hill glass clear enough to hold a wick line steady.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'lantern-rill'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rill-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Oath Broker', 'oath-broker',
       'A broker in simple gray keeps the circle honest by remembering every promise better than its owner.',
       'The broker folds their hands. "An oath is just a future debt with witnesses."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'oath-circle'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oath-broker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Oath Stone', 'oath-stone',
       'A thumb-sized witness stone worn smooth by too many solemn hands.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'oath-circle'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oath-stone' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Fen Listener', 'fen-listener',
       'A reed-wrapped listener stands ankle-deep in black water, hearing the marsh think before anyone else does.',
       'The listener raises one brow. "The fen says plenty. It simply dislikes repetition."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'hush-fen'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fen-listener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Fen Sedge', 'fen-sedge',
       'A dark marsh sedge braided against rot and bad footing.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'hush-fen'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fen-sedge' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Recycler Chief', 'recycler-chief',
       'A salvage chief with scarred gloves and perfect sorting habits can spot useful metal faster than most sensors.',
       'The chief grins at a stripped panel. "Nothing is obsolete until the last person who understands it dies."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'recycler-nest'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'recycler-chief' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Recycler Mesh', 'recycler-mesh',
       'A rolled sheet of conductive salvage mesh, cleaned and stacked for one more useful life.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'recycler-nest'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'recycler-mesh' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rail Marshal', 'rail-marshal',
       'The marshal monitors the dead mag line like it might wake up offended at being underestimated.',
       'Without turning, the marshal says, "Momentum is loyal. It always goes where you pointed it, not where you wished."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'mag-rail-pier'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rail-marshal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Rail Spark', 'rail-spark',
       'A captured magnetic spark crystal in a safety cage no bigger than a plum.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'mag-rail-pier'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rail-spark' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pulse Gardener', 'pulse-gardener',
       'The gardener tends the blinking fruit nodes with insulated shears and alarming affection.',
       'The gardener pats a glowing branch. "Energy is easier to raise when you stop insulting biology."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'pulse-orchard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pulse-gardener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Pulse Fruit', 'pulse-fruit',
       'A warm bioelectric pod that twitches once when picked up.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'pulse-orchard'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pulse-fruit' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Lock Nurse', 'lock-nurse',
       'A quarantine nurse in a faded seal suit checks procedures with the gravity of someone who has seen shortcuts bleed.',
       'The nurse taps the warning stripe. "Clean lines save lives. So do closed doors."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'quarantine-lock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lock-nurse' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Quarantine Tag', 'quarantine-tag',
       'A bright-sealed clearance tag with too many check boxes and exactly one surviving use.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'quarantine-lock'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'quarantine-tag' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Foam Diver', 'foam-diver',
       'A pressure diver moves through the cache with a swimmer''s grace, checking buoyant crates for forgotten value.',
       'The diver flicks amber foam from one glove. "The trick is not to fight the drift. Just be worth where it takes you."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'foam-cache'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'foam-diver' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Foam Amber', 'foam-amber',
       'A pressure-hardened amber bubble full of trapped signal shimmer.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'foam-cache'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'foam-amber' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Switch Diver', 'switch-diver',
       'A timing diver hangs in the current beside the vanes, adjusting flow like a musician tuning a difficult instrument.',
       'The diver laughs softly. "Packets do not mind waiting. People do. Design around the second problem."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'tide-switch'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'switch-diver' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Switch Fuse', 'switch-fuse',
       'A tide-rated switching fuse that still smells faintly of hot copper and sea static.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'tide-switch'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'switch-fuse' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Drift Monk', 'drift-monk',
       'A monk in signal cloth sits cross-legged in the hush current, meditating with admirable disrespect for urgency.',
       'Eyes closed, the monk says, "Silence is bandwidth kept in reserve. Spend it wisely."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'hush-current'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'drift-monk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Hush Algae', 'hush-algae',
       'A ribbon of static-dampening algae that drinks noise out of the water around it.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'hush-current'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hush-algae' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Kelp Surveyor', 'kelp-surveyor',
       'A surveyor trims the coded fronds and keeps the ruined beacon translating itself into useful direction.',
       'The surveyor points at the flashing strands. "Plants make excellent navigators. They never pretend the void is empty."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'beacon-kelp'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'kelp-surveyor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Beacon Kelp', 'beacon-kelp',
       'A trimmed strand of luminous kelp that still blinks a directional pulse along its length.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'beacon-kelp'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'beacon-kelp' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Shrine Keeper', 'shrine-keeper',
       'A grease-stained keeper tends the memorial niche with the solemnity of someone who still believes in maintenance after death.',
       'The keeper straightens a snapped insignia. "Ships are built by crews. So is grief."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'captain-shrine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'shrine-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Shrine Bolt', 'shrine-bolt',
       'A command-grade mounting bolt polished by ritual handling.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'captain-shrine'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'shrine-bolt' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Choir Tech', 'choir-tech',
       'A cryo technician listens to the frost tones with a mechanic''s ear and a conductor''s patience.',
       'The tech nods toward the ringing tubes. "Cold keeps better time than people do."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'cryo-choir'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'choir-tech' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Cryo Chime', 'cryo-chime',
       'A thin glass chime cut from a frost-safe cryo tube collar.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'cryo-choir'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cryo-chime' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ballast Hand', 'ballast-hand',
       'A cargo rigger with scarred knuckles checks the dead grav-hooks as if one might decide to behave today.',
       'The rigger spits into the dark. "Weight is honest. It goes down until you give it a reason not to."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'ballast-maw'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ballast-hand' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ballast Pearl', 'ballast-pearl',
       'A dense grav-pearl used to test load balance in low-light holds.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'ballast-maw'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ballast-pearl' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ash Engineer', 'ash-engineer',
       'An engineer in soot-gray coveralls works the dead drive tunnel like a priest attending a difficult relic.',
       'The engineer wipes one hand clean enough to gesture. "Combustion is just disciplined impatience."',
       false, 0
FROM mud_rooms r WHERE r.slug = 'ash-drive'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ash-engineer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
SELECT r.id, 'Ash Carbon', 'ash-carbon',
       'A scored fin of drive carbon still holding the memory of heat.',
       NULL,
       true, 1
FROM mud_rooms r WHERE r.slug = 'ash-drive'
  AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ash-carbon' AND i.room_id = r.id AND i.owner_character_id IS NULL);

GRANT ALL ON TABLE mud_rooms TO djehuti;
GRANT ALL ON TABLE mud_exits TO djehuti;
GRANT ALL ON TABLE mud_items TO djehuti;
