module Djehuti.Api.MudRepository

open System
open System.Data.Common
open System.Text.Json
open Npgsql
open Database

type MudStats =
    { Presence: int
      Wit: int
      Resolve: int
      Lore: int
      Craft: int
      Guile: int }

type MudSkills =
    { Searching: int
      Crafting: int
      Navigation: int
      Lorekeeping: int
      Negotiation: int
      Devices: int
      Survival: int }

type MudExitView =
    { Direction: string
      ExitType: string
      Label: string option
      TargetRoomId: Guid
      TargetRoomName: string }

type MudMapRoomView =
    { RoomId: Guid
      RoomName: string
      Slug: string
      X: int
      Y: int
      Current: bool
      Visited: bool }

type MudMapExitView =
    { FromRoomId: Guid
      ToRoomId: Guid
      Direction: string
      ExitType: string
      Label: string option }

type MudItemView =
    { Name: string
      Slug: string
      Description: string option
      Portable: bool
      Readable: bool }

type MudRoomState =
    { CharacterId: Guid
      CharacterName: string
      RealmSlug: string
      RealmName: string
      Stats: MudStats
      Skills: MudSkills
      RoomId: Guid
      RoomName: string
      RoomDescription: string option
      ZoneName: string
      MudTierName: string
      VisibleItems: MudItemView list
      InventoryItems: MudItemView list
      MapRooms: MudMapRoomView list
      MapExits: MudMapExitView list
      Exits: MudExitView list
      CurrencyBalance: int
      CurrencyName: string
      CurrencyNamePlural: string
      ArchetypeSlug: string option
      Bio: string option
      PortraitUrl: string option
      Title: string option }

type MudCommandResult =
    { Success: bool
      Command: string
      Message: string
      State: MudRoomState option }

type MudRealmAvailability =
    { RealmSlug: string
      RealmName: string
      CharacterCount: int
      FreeStarterUsed: bool
      CanCreateFreeStarter: bool }

type MudCharacterSummary =
    { Id: Guid
      Name: string
      DisplayName: string
      RealmSlug: string
      RealmName: string
      IsSelected: bool
      CurrentRoomName: string
      MudTierName: string
      InventoryCount: int
      Stats: MudStats
      Skills: MudSkills
      ArchetypeSlug: string option
      Bio: string option
      PortraitUrl: string option
      Title: string option
      CreatedAt: DateTime }

type MudRosterView =
    { SelectedCharacterId: Guid option
      Characters: MudCharacterSummary list
      Realms: MudRealmAvailability list
      PaidSlotsTotal: int
      PaidSlotsUsed: int
      PaidSlotsRemaining: int
      BonusSlots: int }

type MudRealmSummary =
    { Slug: string
      Name: string
      Description: string
      ZoneCount: int
      RoomCount: int }

type MudLandingStats =
    { RoomCount: int
      ZoneCount: int
      RecipeCount: int
      Realms: MudRealmSummary list }

type private MudCharacterRow =
    { Id: Guid
      UserId: Guid
      RealmSlug: string
      Name: string
      DisplayName: string
      CurrentRoomId: Guid
      Stats: MudStats
      Skills: MudSkills
      CreatedAt: DateTime
      UpdatedAt: DateTime }

type private MudUserSettings =
    { ActiveCharacterId: Guid option
      PatreonTierId: string option
      BonusSlots: int }

type private MudRealmDefinition =
    { Slug: string
      Name: string
      StartRoomSlug: string
      Description: string
      CurrencyName: string
      CurrencyNamePlural: string }

type private MudCraftRecipe =
    { Slug: string
      Name: string
      Ingredients: string list
      OutputName: string
      OutputSlug: string
      OutputDescription: string
      OutputReadableText: string option }

type private MudCraftRecipeIngredient =
    { Slug: string
      Quantity: int
      Position: int }

type private MudCraftRecipeDefinition =
    { Slug: string
      Name: string
      Ingredients: MudCraftRecipeIngredient list
      OutputName: string
      OutputSlug: string
      OutputDescription: string
      OutputReadableText: string option
      Position: int
      Active: bool }

let private realmDefinitions =
    [ { Slug = "medieval"; Name = "Medieval"; StartRoomSlug = "keep-gate"
        Description = "Enter at the keep gate. Vaults below, a greenwood beyond the walls, a beacon above the clouds, and a barrow door that asks you to knock on your way out."
        CurrencyName = "crown"; CurrencyNamePlural = "crowns" }
      { Slug = "sci-fi"; Name = "Sci-Fi"; StartRoomSlug = "transit-dock"
        Description = "Dock at Star Reach. Ride the freight lift to the drift ring, board the Vagrant Star, dive into the Signal Sea, or walk the hull out to the Scar."
        CurrencyName = "credit"; CurrencyNamePlural = "credits" }
      { Slug = "the-veil"; Name = "The Veil"; StartRoomSlug = "veil-first-tear"
        Description = "Step through the first tear. Fractured concrete streets, a claustrophobic alley strung with dead neon, and a frayed cage lift that groans down into the static haze."
        CurrencyName = "shard"; CurrencyNamePlural = "shards" }
      { Slug = "the-wild-march"; Name = "The Wild March"; StartRoomSlug = "march-greatroot-landing"
        Description = "Climb into the greatroot canopy. Hollow trunks wide as halls, vine-swallowed altars, and bioluminescent moss lighting the way between root and stone."
        CurrencyName = "resin mark"; CurrencyNamePlural = "resin marks" }
      { Slug = "the-drowned-reach"; Name = "The Drowned Reach"; StartRoomSlug = "reach-first-airlock"
        Description = "Seal the first airlock behind you. A grated stair down into flooded dark, and a glass tunnel where the ocean itself presses in on every side."
        CurrencyName = "pressure chit"; CurrencyNamePlural = "pressure chits" } ]


let private craftRecipes =
    [ { Slug = "torch"
        Name = "Torch"
        Ingredients = [ "rag-strip"; "lamp-oil" ]
        OutputName = "Torch"
        OutputSlug = "torch"
        OutputDescription = "A rough torch made from cloth and oil. It burns hot enough to light dark passages and mark your presence."
        OutputReadableText = None }
      { Slug = "signal-key"
        Name = "Signal Key"
        Ingredients = [ "brass-shard"; "wire-spool" ]
        OutputName = "Signal Key"
        OutputSlug = "signal-key"
        OutputDescription = "A small improvised key of brass and wire. It looks like it belongs in a mechanical slot rather than a lock."
        OutputReadableText = None }
      { Slug = "chalk-charm"
        Name = "Chalk Charm"
        Ingredients = [ "rune-chalk"; "wax-seal" ]
        OutputName = "Chalk Charm"
        OutputSlug = "chalk-charm"
        OutputDescription = "A wax-backed charm marked in chalk. It is more ward than ornament, meant to mark a path someone intends to find again."
        OutputReadableText = Some "Chalk marks wind around the wax in a looping pattern: RETURN, REMEMBER, RETURN." }
      { Slug = "forge-wrap"
        Name = "Forge Wrap"
        Ingredients = [ "rag-strip"; "resin-pitch" ]
        OutputName = "Forge Wrap"
        OutputSlug = "forge-wrap"
        OutputDescription = "A sticky wrap of cloth and resin that can bind handles, seal cracks, or brace a grip for rough work."
        OutputReadableText = None }
      { Slug = "patch-cable"
        Name = "Patch Cable"
        Ingredients = [ "wire-spool"; "capacitor-cell" ]
        OutputName = "Patch Cable"
        OutputSlug = "patch-cable"
        OutputDescription = "A crude data bridge assembled from scavenged wire and a charge cell. It hums when held near powered fittings."
        OutputReadableText = None }
      { Slug = "coolant-beacon"
        Name = "Coolant Beacon"
        Ingredients = [ "crystal-vial"; "coolant-canister" ]
        OutputName = "Coolant Beacon"
        OutputSlug = "coolant-beacon"
        OutputDescription = "A chilled beacon tube that glows pale blue. It looks useful for marking safe routes through hot or unstable machinery."
        OutputReadableText = None }
      { Slug = "field-satchel"
        Name = "Field Satchel"
        Ingredients = [ "fiber-bundle"; "iron-nails" ]
        OutputName = "Field Satchel"
        OutputSlug = "field-satchel"
        OutputDescription = "A reinforced satchel stitched and pinned from whatever was at hand. It is ugly, practical, and made to survive travel."
        OutputReadableText = None }
      { Slug = "ward-lantern"
        Name = "Ward Lantern"
        Ingredients = [ "torch"; "chalk-charm" ]
        OutputName = "Ward Lantern"
        OutputSlug = "ward-lantern"
        OutputDescription = "A torch wrapped in a marked charm. Its light feels deliberate, like it was built to guide someone back through danger."
        OutputReadableText = Some "The chalked wax has been scorched into the cloth: HOLD FAST, RETURN LIT." }
      { Slug = "scribe-kit"
        Name = "Scribe Kit"
        Ingredients = [ "charcoal-stick"; "linen-cord" ]
        OutputName = "Scribe Kit"
        OutputSlug = "scribe-kit"
        OutputDescription = "A practical little writing bundle for making notes, marks, and quick field records."
        OutputReadableText = Some "The cord keeps the charcoal wrapped beside folded scraps for quick notes on the move." }
      { Slug = "lock-bundle"
        Name = "Lock Bundle"
        Ingredients = [ "copper-clasp"; "iron-nails" ]
        OutputName = "Lock Bundle"
        OutputSlug = "lock-bundle"
        OutputDescription = "A bundle of clasps and rough fasteners useful for securing packs, crates, and improvised repairs."
        OutputReadableText = None }
      { Slug = "relay-lantern"
        Name = "Relay Lantern"
        Ingredients = [ "patch-cable"; "glow-filament" ]
        OutputName = "Relay Lantern"
        OutputSlug = "relay-lantern"
        OutputDescription = "A bright relay lamp built from a live patch and a surviving filament. It throws a steady technical glow."
        OutputReadableText = Some "A scratch beside the contact points reads: KEEP THE PATH VISIBLE." }
      { Slug = "hull-patch"
        Name = "Hull Patch"
        Ingredients = [ "alloy-plate"; "sealant-foam" ]
        OutputName = "Hull Patch"
        OutputSlug = "hull-patch"
        OutputDescription = "A quick repair plate backed with expanding sealant. It looks ready for leaks, cracks, or stressed panels."
        OutputReadableText = None }
      { Slug = "star-chart"
        Name = "Star Chart"
        Ingredients = [ "data-shard"; "dock-manifest" ]
        OutputName = "Star Chart"
        OutputSlug = "star-chart"
        OutputDescription = "A reconstructed route chart assembled from fragmented shipping data and surviving dock records."
        OutputReadableText = Some "Recovered route tags spiral around three marked lanes: approach, relay, return." }
      { Slug = "pilgrim-badge"
        Name = "Pilgrim Badge"
        Ingredients = [ "pilgrim-token"; "copper-clasp" ]
        OutputName = "Pilgrim Badge"
        OutputSlug = "pilgrim-badge"
        OutputDescription = "A travel badge clipped together from a token and clasp, worn more for belonging than protection."
        OutputReadableText = None }
      { Slug = "fire-kit"
        Name = "Fire Kit"
        Ingredients = [ "flint-shard"; "tallow-cake" ]
        OutputName = "Fire Kit"
        OutputSlug = "fire-kit"
        OutputDescription = "A striker and tallow bundle that can raise a working flame in wind, damp, or the dark below the keep."
        OutputReadableText = None }
      { Slug = "healers-poultice"
        Name = "Healer's Poultice"
        Ingredients = [ "dried-herbs"; "spring-water" ]
        OutputName = "Healer's Poultice"
        OutputSlug = "healers-poultice"
        OutputDescription = "A damp herb compress steeped in clean well water. Steep, wrap, rest — in that order."
        OutputReadableText = Some "A folded note is tucked into the wrap: HERB AND CLEAN WATER MEND MORE THAN ANY BLADE EVER UNMADE." }
      { Slug = "travel-sling"
        Name = "Travel Sling"
        Ingredients = [ "leather-strap"; "hemp-twine" ]
        OutputName = "Travel Sling"
        OutputSlug = "travel-sling"
        OutputDescription = "An honest carrying sling of cured strap and waxed twine, built to hold when its bearer cannot."
        OutputReadableText = None }
      { Slug = "field-medkit"
        Name = "Field Medkit"
        Ingredients = [ "sterile-gauze"; "nutrient-gel" ]
        OutputName = "Field Medkit"
        OutputSlug = "field-medkit"
        OutputDescription = "A compact triage pouch of gauze and nutrient gel. Stabilize, then move. Clean, then close."
        OutputReadableText = None }
      { Slug = "courier-drone"
        Name = "Courier Drone"
        Ingredients = [ "servo-core"; "beacon-shell" ]
        OutputName = "Courier Drone"
        OutputSlug = "courier-drone"
        OutputDescription = "A beacon shell woken by a live servo core. It hovers at shoulder height, waiting for somewhere to go."
        OutputReadableText = Some "The status ring pulses a single stored instruction: CARRY WORD HOME." }
      { Slug = "cipher-spike"
        Name = "Cipher Spike"
        Ingredients = [ "cipher-tape"; "mag-clamp" ]
        OutputName = "Cipher Spike"
        OutputSlug = "cipher-spike"
        OutputDescription = "A clamp-mounted reader wound with archival cipher tape, made for pulling old broadcasts out of dead racks."
        OutputReadableText = Some "The first legible fragment on the tape reads: RECORD WHAT YOU BUILD. TAPE REMEMBERS WHAT PRIDE FORGETS." }
      { Slug = "forest-salve"
        Name = "Forest Salve"
        Ingredients = [ "willow-bark"; "moon-moss" ]
        OutputName = "Forest Salve"
        OutputSlug = "forest-salve"
        OutputDescription = "A cool green salve of willow and shrine moss. It smells of rain and eases what stings."
        OutputReadableText = None }
      { Slug = "trail-kit"
        Name = "Trail Kit"
        Ingredients = [ "sinew-cord"; "pitch-knot" ]
        OutputName = "Trail Kit"
        OutputSlug = "trail-kit"
        OutputDescription = "A hunter's basics rolled tight: sinew for snares and bindings, pitch knot for a fire that will not quit."
        OutputReadableText = None }
      { Slug = "ink-vessel"
        Name = "Ink Vessel"
        Ingredients = [ "oak-gall"; "river-clay" ]
        OutputName = "Ink Vessel"
        OutputSlug = "ink-vessel"
        OutputDescription = "A small fired-clay pot of dark gall ink, the kind the scriptorium pays for in favors."
        OutputReadableText = Some "A thumb mark is pressed into the clay beside a single word: REMEMBER." }
      { Slug = "void-tether"
        Name = "Void Tether"
        Ingredients = [ "tether-line"; "seal-ring" ]
        OutputName = "Void Tether"
        OutputSlug = "void-tether"
        OutputDescription = "An EVA rig of rated line and fresh seals, made for going outside on purpose and coming back the same way."
        OutputReadableText = None }
      { Slug = "nav-core"
        Name = "Nav Core"
        Ingredients = [ "nav-crystal"; "plasma-cell" ]
        OutputName = "Nav Core"
        OutputSlug = "nav-core"
        OutputDescription = "The Vagrant Star's course crystal seated in a live power housing. Chart and heartbeat in one hand."
        OutputReadableText = Some "When powered, the crystal projects one line of the old course: HEADING TRUE. FINISH THE RUN." }
      { Slug = "survival-pack"
        Name = "Survival Pack"
        Ingredients = [ "thermal-blanket"; "ration-tin" ]
        OutputName = "Survival Pack"
        OutputSlug = "survival-pack"
        OutputDescription = "Warmth and a meal in one bundle — the difference between an emergency and a story you tell later."
        OutputReadableText = None }
      { Slug = "dream-candle"
        Name = "Dream Candle"
        Ingredients = [ "dream-wax"; "ever-tallow" ]
        OutputName = "Dream Candle"
        OutputSlug = "dream-candle"
        OutputDescription = "A candle of barrow wax on an unshrinking wick. Lit at bedside, it burns the sleeper a kinder dream than the one scheduled."
        OutputReadableText = None }
      { Slug = "amber-ward"
        Name = "Amber Ward"
        Ingredients = [ "grave-iron"; "root-amber" ]
        OutputName = "Amber Ward"
        OutputSlug = "amber-ward"
        OutputDescription = "Cold iron set in court amber — the hill's two honest materials in one charm. Bargains read fairer when it is in your pocket."
        OutputReadableText = Some "Scratched inside the amber, very small: THE HILL LET THIS ONE GO." }
      { Slug = "bottled-echo"
        Name = "Bottled Echo"
        Ingredients = [ "echo-water"; "thistle-silk" ]
        OutputName = "Bottled Echo"
        OutputSlug = "bottled-echo"
        OutputDescription = "Echo water stoppered with market silk. Speak into it once, and it will say the words back exactly once, whenever they are needed most."
        OutputReadableText = None }
      { Slug = "star-ingot"
        Name = "Star Ingot"
        Ingredients = [ "star-iron"; "beacon-coal" ]
        OutputName = "Star Ingot"
        OutputSlug = "star-ingot"
        OutputDescription = "Meteoric iron smelted over signal coal. The ingot still remembers a direction, and it is not north."
        OutputReadableText = None }
      { Slug = "message-quill"
        Name = "Message Quill"
        Ingredients = [ "falcon-feather"; "beeswax-block" ]
        OutputName = "Message Quill"
        OutputSlug = "message-quill"
        OutputDescription = "A dropped falcon primary sealed in heather wax. Letters written with it have a way of arriving."
        OutputReadableText = None }
      { Slug = "weather-glass"
        Name = "Weather Glass"
        Ingredients = [ "mist-crystal"; "sky-slate" ]
        OutputName = "Weather Glass"
        OutputSlug = "weather-glass"
        OutputDescription = "A cloudy crystal in a slate frame that shows tomorrow's sky an hour early. The weather stone endorses it, grudgingly."
        OutputReadableText = None }
      { Slug = "signal-locket"
        Name = "Signal Locket"
        Ingredients = [ "static-pearl"; "packet-shell" ]
        OutputName = "Signal Locket"
        OutputSlug = "signal-locket"
        OutputDescription = "A noise pearl seated in a delivered message's empty casing. Worn close, it plays the one transmission you most need to hear again."
        OutputReadableText = None }
      { Slug = "living-index"
        Name = "Living Index"
        Ingredients = [ "index-coral"; "carrier-thread" ]
        OutputName = "Living Index"
        OutputSlug = "living-index"
        OutputDescription = "Catalog coral bound with live carrier thread. Ask it where anything is and it points, smugly, in the right direction."
        OutputReadableText = Some "The coral's newest branch has grown a label: CURRENT LOCATION — WITH YOU." }
      { Slug = "silent-key"
        Name = "Silent Key"
        Ingredients = [ "phantom-code"; "silence-glass" ]
        OutputName = "Silent Key"
        OutputSlug = "silent-key"
        OutputDescription = "Code that finishes nowhere, cast in a pane of kept quiet. It opens things that listen for footsteps."
        OutputReadableText = None }
      { Slug = "witness-lens"
        Name = "Witness Lens"
        Ingredients = [ "scar-glass"; "lens-shard" ]
        OutputName = "Witness Lens"
        OutputSlug = "witness-lens"
        OutputDescription = "The scar's fused glass ground against the navigator's blister shard. Look through it at any damage and see the moment it happened. Use sparingly."
        OutputReadableText = Some "Etched on the rim in a careful hand: SOME ANSWERS ARE INJURIES. LOOK ANYWAY. — V.S." }
      { Slug = "barnacle-anchor"
        Name = "Barnacle Anchor"
        Ingredients = [ "void-barnacle"; "mag-bearing" ]
        OutputName = "Barnacle Anchor"
        OutputSlug = "barnacle-anchor"
        OutputDescription = "A void barnacle colony mounted on a frictionless bearing: it grips anything, releases on a twist, and grows fonder of you over time."
        OutputReadableText = None }
      { Slug = "solar-kite"
        Name = "Solar Kite"
        Ingredients = [ "vane-foil"; "aerial-wire" ]
        OutputName = "Solar Kite"
        OutputSlug = "solar-kite"
        OutputDescription = "Bright foil on a singing wire frame. Flown in any light, it tugs toward the nearest star like it knows something."
        OutputReadableText = None }
      { Slug = "orchard-tonic"
        Name = "Orchard Tonic"
        Ingredients = [ "orchard-apple"; "spring-water" ]
        OutputName = "Orchard Tonic"
        OutputSlug = "orchard-tonic"
        OutputDescription = "A bright restorative drink of pressed orchard apple and cold spring water. It tastes like a decision to keep going."
        OutputReadableText = Some "A neat chalk note circles the stopper: FOR LONG ROADS, HARD STAIRS, AND ANY DAY THAT STARTED TOO EARLY." }
      { Slug = "roost-signet"
        Name = "Roost Signet"
        Ingredients = [ "falcon-feather"; "copper-clasp" ]
        OutputName = "Roost Signet"
        OutputSlug = "roost-signet"
        OutputDescription = "A feather clasped into a travel badge, carried by messengers who prefer the sky to the road."
        OutputReadableText = Some "The reverse is scratched with one promise: IF IT FLIES, IT RETURNS." }
      { Slug = "barrow-lamp"
        Name = "Barrow Lamp"
        Ingredients = [ "dream-wax"; "lamp-oil" ]
        OutputName = "Barrow Lamp"
        OutputSlug = "barrow-lamp"
        OutputDescription = "A slow-burning lamp fed by dream wax and old oil. It keeps a calm flame where ordinary fire grows nervous."
        OutputReadableText = None }
      { Slug = "weather-ribbon"
        Name = "Weather Ribbon"
        Ingredients = [ "mist-crystal"; "linen-cord" ]
        OutputName = "Weather Ribbon"
        OutputSlug = "weather-ribbon"
        OutputDescription = "A corded crystal streamer that twists toward tomorrow's wind before the rest of the valley notices."
        OutputReadableText = Some "The ribbon flutters a warning in one direction only: rain before dusk." }
      { Slug = "sunspool"
        Name = "Sunspool"
        Ingredients = [ "vane-foil"; "wire-spool" ]
        OutputName = "Sunspool"
        OutputSlug = "sunspool"
        OutputDescription = "A tight spool of reflective foil and fine wire used to throw light into places that do not deserve the dark."
        OutputReadableText = None }
      { Slug = "packet-lure"
        Name = "Packet Lure"
        Ingredients = [ "packet-shell"; "glow-filament" ]
        OutputName = "Packet Lure"
        OutputSlug = "packet-lure"
        OutputDescription = "A blinking shell lure that draws service drones and curious signals out of hiding."
        OutputReadableText = Some "Boot prompt: PING ONCE. WAIT. LET CURIOSITY DO THE WALKING." }
      { Slug = "bearing-hook"
        Name = "Bearing Hook"
        Ingredients = [ "mag-bearing"; "alloy-plate" ]
        OutputName = "Bearing Hook"
        OutputSlug = "bearing-hook"
        OutputDescription = "A magnetic utility hook built for hull work, salvage pulls, and stubborn cargo."
        OutputReadableText = None }
      { Slug = "hush-visor"
        Name = "Hush Visor"
        Ingredients = [ "silence-glass"; "fiber-bundle" ]
        OutputName = "Hush Visor"
        OutputSlug = "hush-visor"
        OutputDescription = "A woven visor plated with silence glass. It blunts glare, static, and conversations you never agreed to hear."
        OutputReadableText = None }
      { Slug = "stable-kit"
        Name = "Stable Kit"
        Ingredients = [ "stable-nails"; "leather-strap" ]
        OutputName = "Stable Kit"
        OutputSlug = "stable-kit"
        OutputDescription = "A rolled stable repair bundle with nails, strap, and the smell of horses and weather."
        OutputReadableText = None }
      { Slug = "farrier-mark"
        Name = "Farrier Mark"
        Ingredients = [ "hoof-iron"; "copper-clasp" ]
        OutputName = "Farrier Mark"
        OutputSlug = "farrier-mark"
        OutputDescription = "A stamped token of worked iron clipped with a clasp. Carters treat it as proof that your kit is in order."
        OutputReadableText = Some "One side bears a tiny hammered anvil and the words: SHOD, FED, READY." }
      { Slug = "cistern-draught"
        Name = "Cistern Draught"
        Ingredients = [ "cistern-salt"; "spring-water" ]
        OutputName = "Cistern Draught"
        OutputSlug = "cistern-draught"
        OutputDescription = "A mineral tonic mixed from clean water and old ward salt. It tastes like stone, then strength."
        OutputReadableText = None }
      { Slug = "silk-bandolier"
        Name = "Silk Bandolier"
        Ingredients = [ "spool-silk"; "hemp-twine" ]
        OutputName = "Silk Bandolier"
        OutputSlug = "silk-bandolier"
        OutputDescription = "A cross-body carry strap braided from market silk and field twine. Lighter than it has any right to be."
        OutputReadableText = None }
      { Slug = "fern-poultice"
        Name = "Fern Poultice"
        Ingredients = [ "fern-frond"; "dried-herbs" ]
        OutputName = "Fern Poultice"
        OutputSlug = "fern-poultice"
        OutputDescription = "A cool woodland poultice that smells green even after dark."
        OutputReadableText = None }
      { Slug = "moonreed-flute"
        Name = "Moonreed Flute"
        Ingredients = [ "moon-reed"; "river-clay" ]
        OutputName = "Moonreed Flute"
        OutputSlug = "moonreed-flute"
        OutputDescription = "A narrow reed flute sealed in river clay. It plays best beside water and poorly under orders."
        OutputReadableText = Some "A fingernail score along the stem reads: CALL ONLY WHAT YOU CAN GREET." }
      { Slug = "briar-hook"
        Name = "Briar Hook"
        Ingredients = [ "briar-thorn"; "sinew-cord" ]
        OutputName = "Briar Hook"
        OutputSlug = "briar-hook"
        OutputDescription = "A thorned field hook tied off with cured sinew. Excellent for pulling what does not want to be found."
        OutputReadableText = None }
      { Slug = "owl-charm"
        Name = "Owl Charm"
        Ingredients = [ "owl-pellet"; "rune-chalk" ]
        OutputName = "Owl Charm"
        OutputSlug = "owl-charm"
        OutputDescription = "A chalk-marked bundle that keeps stillness close and foolish noise farther away."
        OutputReadableText = Some "Drawn in chalk around the wrapping: WATCH FIRST. STEP SECOND." }
      { Slug = "grave-incense"
        Name = "Grave Incense"
        Ingredients = [ "grave-bloom"; "tallow-cake" ]
        OutputName = "Grave Incense"
        OutputSlug = "grave-incense"
        OutputDescription = "A slow smoke of hillflower and rendered tallow, used when you want the dead calm rather than curious."
        OutputReadableText = None }
      { Slug = "rill-lantern"
        Name = "Rill Lantern"
        Ingredients = [ "rill-glass"; "lamp-oil" ]
        OutputName = "Rill Lantern"
        OutputSlug = "rill-lantern"
        OutputDescription = "A clear lantern of stream glass that turns even nervous light soft and usable."
        OutputReadableText = None }
      { Slug = "oath-token"
        Name = "Oath Token"
        Ingredients = [ "oath-stone"; "brass-shard" ]
        OutputName = "Oath Token"
        OutputSlug = "oath-token"
        OutputDescription = "A speaking-stone chip set in brass. It feels heavier in the hand whenever you lie."
        OutputReadableText = Some "The brass lip is etched with a warning: SPEAK ONLY WHAT YOU CAN CARRY." }
      { Slug = "fen-cloak"
        Name = "Fen Cloak"
        Ingredients = [ "fen-sedge"; "linen-cord" ]
        OutputName = "Fen Cloak"
        OutputSlug = "fen-cloak"
        OutputDescription = "A marsh weave that sheds mist, hangs light, and smells like someplace mapmakers avoid."
        OutputReadableText = None }
      { Slug = "recycler-coil"
        Name = "Recycler Coil"
        Ingredients = [ "recycler-mesh"; "capacitor-cell" ]
        OutputName = "Recycler Coil"
        OutputSlug = "recycler-coil"
        OutputDescription = "A jury-rigged recycler coil with one more honest use left in it."
        OutputReadableText = None }
      { Slug = "rail-lure"
        Name = "Rail Lure"
        Ingredients = [ "rail-spark"; "mag-clamp" ]
        OutputName = "Rail Lure"
        OutputSlug = "rail-lure"
        OutputDescription = "A magnetic lure that throws a quick flash and a cleaner line than panic ever will."
        OutputReadableText = None }
      { Slug = "pulse-battery"
        Name = "Pulse Battery"
        Ingredients = [ "pulse-fruit"; "coolant-canister" ]
        OutputName = "Pulse Battery"
        OutputSlug = "pulse-battery"
        OutputDescription = "A bioelectric reserve wrapped in coolant skin. The station gardeners swear it is safer than it looks."
        OutputReadableText = Some "Printed on the improvised casing: FEED LOW DRAW ONLY. DO NOT TAUNT." }
      { Slug = "quarantine-seal"
        Name = "Quarantine Seal"
        Ingredients = [ "quarantine-tag"; "sealant-foam" ]
        OutputName = "Quarantine Seal"
        OutputSlug = "quarantine-seal"
        OutputDescription = "A bright emergency seal for lockers, wounds, doors, or any situation that improves with one firm boundary."
        OutputReadableText = None }
      { Slug = "foam-lantern"
        Name = "Foam Lantern"
        Ingredients = [ "foam-amber"; "glow-filament" ]
        OutputName = "Foam Lantern"
        OutputSlug = "foam-lantern"
        OutputDescription = "A softly glowing bulb of tide amber wound with filament. It shines like a memory that survived vacuum."
        OutputReadableText = None }
      { Slug = "tide-key"
        Name = "Tide Key"
        Ingredients = [ "switch-fuse"; "data-shard" ]
        OutputName = "Tide Key"
        OutputSlug = "tide-key"
        OutputDescription = "A switch fuse encoded with old sea logic. It opens systems that still think in currents."
        OutputReadableText = Some "Recovered command line: WAIT FOR THE TIDE. THEN TURN." }
      { Slug = "hush-weave"
        Name = "Hush Weave"
        Ingredients = [ "hush-algae"; "fiber-bundle" ]
        OutputName = "Hush Weave"
        OutputSlug = "hush-weave"
        OutputDescription = "A quiet fabric spun from sea growth and ship fiber. It absorbs static and small mistakes."
        OutputReadableText = None }
      { Slug = "beacon-antenna"
        Name = "Beacon Antenna"
        Ingredients = [ "beacon-kelp"; "aerial-wire" ]
        OutputName = "Beacon Antenna"
        OutputSlug = "beacon-antenna"
        OutputDescription = "A flexible signal wand grown from pressure kelp and tuned with aerial wire."
        OutputReadableText = None }
      { Slug = "shrine-relay"
        Name = "Shrine Relay"
        Ingredients = [ "shrine-bolt"; "patch-cable" ]
        OutputName = "Shrine Relay"
        OutputSlug = "shrine-relay"
        OutputDescription = "A votive relay node built from a surviving captain's bolt and a practical cable. Reverent, but not sentimental."
        OutputReadableText = Some "The relay cycles one old shipboard blessing: MAY THE RETURN BE CLEAN." }
      { Slug = "cryo-bell"
        Name = "Cryo Bell"
        Ingredients = [ "cryo-chime"; "silence-glass" ]
        OutputName = "Cryo Bell"
        OutputSlug = "cryo-bell"
        OutputDescription = "A tone vessel cut to ring through frost without shattering it."
        OutputReadableText = None }
      { Slug = "ballast-anchor"
        Name = "Ballast Anchor"
        Ingredients = [ "ballast-pearl"; "mag-bearing" ]
        OutputName = "Ballast Anchor"
        OutputSlug = "ballast-anchor"
        OutputDescription = "A dense little anchor head that settles whatever line, thought, or cargo you attach it to."
        OutputReadableText = None }
      { Slug = "ash-tablet"
        Name = "Ash Tablet"
        Ingredients = [ "ash-carbon"; "lens-shard" ]
        OutputName = "Ash Tablet"
        OutputSlug = "ash-tablet"
        OutputDescription = "A black recording slate that takes notes in pale etched lines and never seems impressed by them."
        OutputReadableText = None } ]

let private bootstrapRecipeDefinitions =
    craftRecipes
    |> List.mapi (fun index recipe ->
        { Slug = recipe.Slug
          Name = recipe.Name
          Ingredients =
            recipe.Ingredients
            |> List.mapi (fun ingredientIndex slug ->
                { Slug = slug
                  Quantity = 1
                  Position = ingredientIndex })
          OutputName = recipe.OutputName
          OutputSlug = recipe.OutputSlug
          OutputDescription = recipe.OutputDescription
          OutputReadableText = recipe.OutputReadableText
          Position = index
          Active = true })

let private insertBootstrapRecipe (conn: NpgsqlConnection) (recipe: MudCraftRecipeDefinition) =
    use recipeCmd = new NpgsqlCommand(
        """INSERT INTO mud_craft_recipes (slug, name, output_name, output_slug, output_description, output_readable_text, sort_order, active)
           VALUES (@slug, @name, @output_name, @output_slug, @output_description, @output_readable_text, @sort_order, @active)
           ON CONFLICT (slug) DO UPDATE
           SET name = EXCLUDED.name,
               output_name = EXCLUDED.output_name,
               output_slug = EXCLUDED.output_slug,
               output_description = EXCLUDED.output_description,
               output_readable_text = EXCLUDED.output_readable_text,
               sort_order = EXCLUDED.sort_order,
               active = EXCLUDED.active
           RETURNING id""", conn)
    recipeCmd.Parameters.AddWithValue("slug", recipe.Slug) |> ignore
    recipeCmd.Parameters.AddWithValue("name", recipe.Name) |> ignore
    recipeCmd.Parameters.AddWithValue("output_name", recipe.OutputName) |> ignore
    recipeCmd.Parameters.AddWithValue("output_slug", recipe.OutputSlug) |> ignore
    recipeCmd.Parameters.AddWithValue("output_description", recipe.OutputDescription) |> ignore
    recipeCmd.Parameters.AddWithValue("output_readable_text", recipe.OutputReadableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    recipeCmd.Parameters.AddWithValue("sort_order", recipe.Position) |> ignore
    recipeCmd.Parameters.AddWithValue("active", recipe.Active) |> ignore
    let recipeId = recipeCmd.ExecuteScalar() :?> Guid

    use deleteIngredientsCmd = new NpgsqlCommand("DELETE FROM mud_craft_recipe_ingredients WHERE recipe_id = @recipe_id", conn)
    deleteIngredientsCmd.Parameters.AddWithValue("recipe_id", recipeId) |> ignore
    deleteIngredientsCmd.ExecuteNonQuery() |> ignore

    for ingredient in recipe.Ingredients do
        use ingredientCmd = new NpgsqlCommand(
            """INSERT INTO mud_craft_recipe_ingredients (recipe_id, ingredient_slug, quantity, position)
               VALUES (@recipe_id, @ingredient_slug, @quantity, @position)""", conn)
        ingredientCmd.Parameters.AddWithValue("recipe_id", recipeId) |> ignore
        ingredientCmd.Parameters.AddWithValue("ingredient_slug", ingredient.Slug) |> ignore
        ingredientCmd.Parameters.AddWithValue("quantity", ingredient.Quantity) |> ignore
        ingredientCmd.Parameters.AddWithValue("position", ingredient.Position) |> ignore
        ingredientCmd.ExecuteNonQuery() |> ignore

let ensureCraftRecipeCatalogSeeded (conn: NpgsqlConnection) =
    try
        use countCmd = new NpgsqlCommand("SELECT COUNT(*) FROM mud_craft_recipes", conn)
        let recipeCount = countCmd.ExecuteScalar() :?> int64
        if recipeCount = 0L then
            for recipe in bootstrapRecipeDefinitions do
                insertBootstrapRecipe conn recipe
        true
    with _ ->
        false

let private loadCraftRecipes (conn: NpgsqlConnection) =
    if not (ensureCraftRecipeCatalogSeeded conn) then
        bootstrapRecipeDefinitions
    else
        use cmd = new NpgsqlCommand(
            """SELECT r.slug,
                      r.name,
                      r.output_name,
                      r.output_slug,
                      r.output_description,
                      r.output_readable_text,
                      r.sort_order,
                      r.active,
                      i.ingredient_slug,
                      i.quantity,
                      i.position
               FROM mud_craft_recipes r
               LEFT JOIN mud_craft_recipe_ingredients i ON i.recipe_id = r.id
               WHERE r.active = TRUE
               ORDER BY r.sort_order, r.name, i.position, i.ingredient_slug""", conn)
        use reader = cmd.ExecuteReader()
        let recipes = ResizeArray<MudCraftRecipeDefinition>()
        let bySlug = Collections.Generic.Dictionary<string, int>()

        while reader.Read() do
            let slug = reader.GetString(0)
            let recipeIndex =
                match bySlug.TryGetValue(slug) with
                | true, index -> index
                | false, _ ->
                    let index = recipes.Count
                    recipes.Add(
                        { Slug = slug
                          Name = reader.GetString(1)
                          Ingredients = []
                          OutputName = reader.GetString(2)
                          OutputSlug = reader.GetString(3)
                          OutputDescription = reader.GetString(4)
                          OutputReadableText = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                          Position = reader.GetInt32(6)
                          Active = reader.GetBoolean(7) })
                    bySlug.Add(slug, index)
                    index

            if not (reader.IsDBNull(8)) then
                let existing = recipes.[recipeIndex]
                let ingredient =
                    { Slug = reader.GetString(8)
                      Quantity = reader.GetInt32(9)
                      Position = reader.GetInt32(10) }
                recipes.[recipeIndex] <- { existing with Ingredients = existing.Ingredients @ [ ingredient ] }

        recipes |> Seq.toList

let private defaultStats =
    { Presence = 1
      Wit = 1
      Resolve = 1
      Lore = 1
      Craft = 1
      Guile = 1 }

let private defaultSkills =
    { Searching = 1
      Crafting = 1
      Navigation = 1
      Lorekeeping = 1
      Negotiation = 1
      Devices = 1
      Survival = 1 }

let private readStats (r: DbDataReader) startIndex =
    { Presence = r.GetInt32(startIndex)
      Wit = r.GetInt32(startIndex + 1)
      Resolve = r.GetInt32(startIndex + 2)
      Lore = r.GetInt32(startIndex + 3)
      Craft = r.GetInt32(startIndex + 4)
      Guile = r.GetInt32(startIndex + 5) }

let private readSkills (r: DbDataReader) startIndex =
    { Searching = r.GetInt32(startIndex)
      Crafting = r.GetInt32(startIndex + 1)
      Navigation = r.GetInt32(startIndex + 2)
      Lorekeeping = r.GetInt32(startIndex + 3)
      Negotiation = r.GetInt32(startIndex + 4)
      Devices = r.GetInt32(startIndex + 5)
      Survival = r.GetInt32(startIndex + 6) }

let private readCharacter (r: DbDataReader) =
    { Id = r.GetGuid(0)
      UserId = r.GetGuid(1)
      RealmSlug = r.GetString(2)
      Name = r.GetString(3)
      DisplayName = r.GetString(4)
      CurrentRoomId = r.GetGuid(5)
      Stats = readStats r 6
      Skills = readSkills r 12
      CreatedAt = r.GetFieldValue<DateTime>(19)
      UpdatedAt = r.GetFieldValue<DateTime>(20) }

let private readStateBase (r: DbDataReader) =
    { CharacterId = r.GetGuid(0)
      CharacterName = r.GetString(1)
      RealmSlug = r.GetString(2)
      RealmName = r.GetString(3)
      Stats = readStats r 4
      Skills = readSkills r 10
      RoomId = r.GetGuid(17)
      RoomName = r.GetString(18)
      RoomDescription = if r.IsDBNull(19) then None else Some (r.GetString(19))
      ZoneName = r.GetString(20)
      MudTierName = "Wanderer"
      VisibleItems = []
      InventoryItems = []
      MapRooms = []
      MapExits = []
      Exits = []
      CurrencyBalance = 0
      CurrencyName = ""
      CurrencyNamePlural = ""
      ArchetypeSlug = if r.IsDBNull(21) then None else Some (r.GetString(21))
      Bio = if r.IsDBNull(22) then None else Some (r.GetString(22))
      PortraitUrl = if r.IsDBNull(23) then None else Some (r.GetString(23))
      Title = None }

let private readItemView (r: DbDataReader) =
    { Name = r.GetString(0)
      Slug = r.GetString(1)
      Description = if r.IsDBNull(2) then None else Some (r.GetString(2))
      Portable = r.GetBoolean(3)
      Readable = not (r.IsDBNull(4)) }

let private nonBlank (value: string option) =
    value |> Option.bind (fun s -> if String.IsNullOrWhiteSpace(s) then None else Some s)

let private normalizeRealmSlug (value: string) =
    value.Trim().ToLowerInvariant()

let private realmBySlug slug =
    realmDefinitions
    |> List.tryFind (fun realm -> realm.Slug = normalizeRealmSlug slug)

// Character creation: archetypes and point-buy stat allocation.
//
// Every stat used to be hardcoded to 1 for every character. Creation now
// works in two layers: an archetype (a realm-specific background) applies
// a fixed bonus to two of the six stats and grants a themed starter item;
// the player then distributes a small free-point pool across all six
// stats, within a per-stat cap. See the MUD Multi-Character System wiki
// page for the design rationale.

type MudArchetype =
    { Slug: string
      RealmSlug: string
      Name: string
      Description: string
      StatBonusPresence: int
      StatBonusWit: int
      StatBonusResolve: int
      StatBonusLore: int
      StatBonusCraft: int
      StatBonusGuile: int
      StarterItemName: string
      StarterItemSlug: string
      StarterItemDescription: string }

let [<Literal>] StarterCurrencyGrant = 15

let private archetypeDefinitions =
    [ // Medieval
      { Slug = "squire"; RealmSlug = "medieval"; Name = "Squire"
        Description = "Trained young at the keep, more comfortable with a blade drill than a book."
        StatBonusPresence = 2; StatBonusWit = 0; StatBonusResolve = 1; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 0
        StarterItemName = "Practice Sword"; StarterItemSlug = "practice-sword"
        StarterItemDescription = "A dulled blade worn smooth from drilling. Not sharp, but familiar in the hand." }
      { Slug = "hedge-witch"; RealmSlug = "medieval"; Name = "Hedge Witch"
        Description = "Learned herbcraft and half-remembered rites from an aunt who never wrote anything down."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 0; StatBonusLore = 2; StatBonusCraft = 1; StatBonusGuile = 0
        StarterItemName = "Herb Pouch"; StarterItemSlug = "herb-pouch"
        StarterItemDescription = "A small satchel of dried herbs, sorted by smell more than name." }
      { Slug = "vagabond"; RealmSlug = "medieval"; Name = "Vagabond"
        Description = "Slept in more hedgerows than beds, and got good at moving on before anyone asked why."
        StatBonusPresence = 0; StatBonusWit = 1; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 2
        StarterItemName = "Lockpick Set"; StarterItemSlug = "lockpick-set"
        StarterItemDescription = "A worn set of picks, tucked into a strip of oiled leather." }

      // Sci-Fi
      { Slug = "engineer"; RealmSlug = "sci-fi"; Name = "Engineer"
        Description = "Cut their teeth keeping third-hand equipment running past its rated life."
        StatBonusPresence = 0; StatBonusWit = 1; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 2; StatBonusGuile = 0
        StarterItemName = "Multitool"; StarterItemSlug = "multitool"
        StarterItemDescription = "A compact folding tool, most of its edges worn smooth from use." }
      { Slug = "pilot"; RealmSlug = "sci-fi"; Name = "Pilot"
        Description = "Flew transports nobody else wanted the routes for, and lived to complain about it."
        StatBonusPresence = 2; StatBonusWit = 0; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 1
        StarterItemName = "Flight Goggles"; StarterItemSlug = "flight-goggles"
        StarterItemDescription = "Scratched but functional, one lens tinted from an old repair." }
      { Slug = "xenobiologist"; RealmSlug = "sci-fi"; Name = "Xenobiologist"
        Description = "Catalogued things that were never meant to survive shipping, and mostly got away with it."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 1; StatBonusLore = 2; StatBonusCraft = 0; StatBonusGuile = 0
        StarterItemName = "Sample Kit"; StarterItemSlug = "sample-kit"
        StarterItemDescription = "Sterile vials and a hand scanner, still smelling faintly of antiseptic." }

      // The Veil
      { Slug = "seam-walker"; RealmSlug = "the-veil"; Name = "Seam-Walker"
        Description = "Learned to read the fractures before the fractures learned to read back."
        StatBonusPresence = 0; StatBonusWit = 1; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 2
        StarterItemName = "Bent Compass"; StarterItemSlug = "bent-compass"
        StarterItemDescription = "The needle never points north here. It points toward the nearest fracture instead." }
      { Slug = "scrap-cartographer"; RealmSlug = "the-veil"; Name = "Scrap Cartographer"
        Description = "Keeps drawing maps of streets that keep moving, and refuses to stop."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 0; StatBonusLore = 2; StatBonusCraft = 1; StatBonusGuile = 0
        StarterItemName = "Torn Map"; StarterItemSlug = "torn-map"
        StarterItemDescription = "Hand-annotated, half the notes crossed out and replaced more than once." }
      { Slug = "lightbreaker"; RealmSlug = "the-veil"; Name = "Lightbreaker"
        Description = "Stood too close to a jagged beam once and came back different. Better, probably."
        StatBonusPresence = 1; StatBonusWit = 0; StatBonusResolve = 2; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 0
        StarterItemName = "Cracked Lens"; StarterItemSlug = "cracked-lens"
        StarterItemDescription = "Looking through it bends the neon glow into something almost readable." }

      // The Wild March
      { Slug = "rootrunner"; RealmSlug = "the-wild-march"; Name = "Rootrunner"
        Description = "Grew up moving fast along root and vine, well ahead of anything that might be following."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 1; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 2
        StarterItemName = "Vine Rope"; StarterItemSlug = "vine-rope"
        StarterItemDescription = "A coil of tough, still-living vine. It grips better than any rope should." }
      { Slug = "mosskeepers-apprentice"; RealmSlug = "the-wild-march"; Name = "Mosskeeper's Apprentice"
        Description = "Spent years learning which moss glows for light and which glows as a warning."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 0; StatBonusLore = 2; StatBonusCraft = 1; StatBonusGuile = 0
        StarterItemName = "Moss Satchel"; StarterItemSlug = "moss-satchel"
        StarterItemDescription = "A satchel lined with damp moss, kept alive and faintly glowing." }
      { Slug = "wardens-ward"; RealmSlug = "the-wild-march"; Name = "Warden's Ward"
        Description = "Raised under a warden's eye, and never quite lost the habit of watching the treeline."
        StatBonusPresence = 2; StatBonusWit = 1; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 0
        StarterItemName = "Carved Whistle"; StarterItemSlug = "carved-whistle"
        StarterItemDescription = "Carved from pale bark. It carries further than a whistle that size should." }

      // The Drowned Reach
      { Slug = "diver"; RealmSlug = "the-drowned-reach"; Name = "Diver"
        Description = "Spent more hours under pressure than above it, and stopped noticing the cold."
        StatBonusPresence = 0; StatBonusWit = 0; StatBonusResolve = 2; StatBonusLore = 0; StatBonusCraft = 1; StatBonusGuile = 0
        StarterItemName = "Diving Mask"; StarterItemSlug = "diving-mask"
        StarterItemDescription = "Scratched at the edges but the seal still holds." }
      { Slug = "signal-tech"; RealmSlug = "the-drowned-reach"; Name = "Signal Tech"
        Description = "Keeps the comms alive between habitats, mostly through stubbornness and spare parts."
        StatBonusPresence = 0; StatBonusWit = 2; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 1; StatBonusGuile = 0
        StarterItemName = "Signal Beacon"; StarterItemSlug = "signal-beacon"
        StarterItemDescription = "A hand-held pulse beacon, dented but still keeping time." }
      { Slug = "salvager"; RealmSlug = "the-drowned-reach"; Name = "Salvager"
        Description = "Makes a living pulling useful things out of wrecks nobody else will go near."
        StatBonusPresence = 1; StatBonusWit = 0; StatBonusResolve = 0; StatBonusLore = 0; StatBonusCraft = 0; StatBonusGuile = 2
        StarterItemName = "Salvage Hook"; StarterItemSlug = "salvage-hook"
        StarterItemDescription = "A hooked pole for pulling wreckage close without getting close to it yourself." } ]

let getArchetypes () = archetypeDefinitions

let private archetypeBySlug (realmSlug: string) (slug: string) =
    archetypeDefinitions
    |> List.tryFind (fun a -> a.RealmSlug = normalizeRealmSlug realmSlug && a.Slug = slug.Trim().ToLowerInvariant())

/// Each base stat and the bonus-allocation pool are rolled the same way:
/// sum of three dice (1-6), i.e. classic 3d6 (range 3-18 per roll).
let private rollD6 () = System.Random.Shared.Next(1, 7)
let private roll3d6 () = rollD6 () + rollD6 () + rollD6 ()

type StatRoll =
    { Stats: MudStats
      BonusPool: int }

let rollCharacterStats () : StatRoll =
    { Stats =
        { Presence = roll3d6 ()
          Wit = roll3d6 ()
          Resolve = roll3d6 ()
          Lore = roll3d6 ()
          Craft = roll3d6 ()
          Guile = roll3d6 () }
      BonusPool = roll3d6 () }

/// Pending rolls are held in memory only (per userId, one at a time) --
/// no reroll once a character is created, and losing a pending roll to a
/// deploy/restart just means the player rolls again, so this doesn't need
/// to be durable.
let private pendingStatRolls = System.Collections.Concurrent.ConcurrentDictionary<Guid, StatRoll>()

let rollStatsForUser (userId: Guid) : StatRoll =
    let roll = rollCharacterStats ()
    pendingStatRolls.[userId] <- roll
    roll

let private validateStatAllocation (archetype: MudArchetype) (roll: StatRoll) (allocation: MudStats) =
    let values = [ allocation.Presence; allocation.Wit; allocation.Resolve; allocation.Lore; allocation.Craft; allocation.Guile ]
    if values |> List.exists (fun v -> v < 0) then
        Error "Stat points cannot be negative."
    elif List.sum values <> roll.BonusPool then
        Error $"You must allocate exactly {roll.BonusPool} points (your rolled bonus pool) across your stats."
    else
        Ok
            { Presence = roll.Stats.Presence + archetype.StatBonusPresence + allocation.Presence
              Wit = roll.Stats.Wit + archetype.StatBonusWit + allocation.Wit
              Resolve = roll.Stats.Resolve + archetype.StatBonusResolve + allocation.Resolve
              Lore = roll.Stats.Lore + archetype.StatBonusLore + allocation.Lore
              Craft = roll.Stats.Craft + archetype.StatBonusCraft + allocation.Craft
              Guile = roll.Stats.Guile + archetype.StatBonusGuile + allocation.Guile }

let private currencyNames slug =
    match realmBySlug slug with
    | Some realm -> realm.CurrencyName, realm.CurrencyNamePlural
    | None -> "coin", "coins"

let private getCurrencyBalance (conn: NpgsqlConnection) (characterId: Guid) (realmSlug: string) : int =
    use cmd = new NpgsqlCommand(
        """SELECT balance FROM mud_character_currency
           WHERE character_id = @character_id AND realm_slug = @realm_slug""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("realm_slug", realmSlug) |> ignore
    match cmd.ExecuteScalar() with
    | null -> 0
    | value -> value :?> int

/// Adjusts a character's realm-scoped currency balance by `delta`
/// (positive to credit, negative to debit). Refuses to go negative -
/// returns the resulting balance, or an error naming the shortfall.
let private adjustCurrency (conn: NpgsqlConnection) (characterId: Guid) (realmSlug: string) (delta: int) : Result<int, string> =
    let current = getCurrencyBalance conn characterId realmSlug
    let next = current + delta
    if next < 0 then
        Error "not enough to cover that"
    else
        use cmd = new NpgsqlCommand(
            """INSERT INTO mud_character_currency (character_id, realm_slug, balance)
               VALUES (@character_id, @realm_slug, @balance)
               ON CONFLICT (character_id, realm_slug) DO UPDATE
               SET balance = @balance, updated_at = now()""", conn)
        cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        cmd.Parameters.AddWithValue("realm_slug", realmSlug) |> ignore
        cmd.Parameters.AddWithValue("balance", next) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        Ok next

let private realmName slug =
    realmBySlug slug
    |> Option.map _.Name
    |> Option.defaultValue slug

let private payloadOf (pairs: (string * string) list) =
    let rendered =
        pairs
        |> List.map (fun (key, value) -> $"\"{key}\":{JsonSerializer.Serialize(value)}")
        |> String.concat ","
    "{" + rendered + "}"

let private loadMudTierName (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT COALESCE(mtl.mud_name, pt.tier_name, 'Wanderer')
           FROM users u
           LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
           LEFT JOIN mud_tier_labels mtl ON mtl.patreon_tier_id = pt.tier_id
           WHERE u.id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    let scalar = cmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then "Wanderer" else scalar :?> string

let private deriveTitle (userId: Guid) : string option =
    AchievementRepository.getUserAchievements userId
    |> List.filter (fun a -> a.Category = "mud")
    |> List.sortByDescending (fun a -> a.Points, a.AwardedAt)
    |> List.tryHead
    |> Option.map _.Name

type MudRealmCharacterView =
    { CharacterId: Guid
      DisplayName: string
      PortraitUrl: string option
      Title: string option
      CurrentRoomName: string
      IsSelf: bool }

/// Characters currently active (same online window used for chat presence)
/// in the given realm, regardless of which room they're in.
let getRealmRoster (realmSlug: string) (viewerCharacterId: Guid option) : MudRealmCharacterView list =
    let normalizedRealm = normalizeRealmSlug realmSlug
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT c.id, c.display_name, c.portrait_url, r.name, u.id
           FROM mud_characters c
           JOIN users u ON u.active_mud_character_id = c.id
           JOIN mud_rooms r ON r.id = c.current_room_id
           WHERE c.realm_slug = @realm_slug
             AND c.deleted_at IS NULL
             AND c.last_active_at > now() - make_interval(mins => @minutes)
           ORDER BY c.display_name""", conn)
    cmd.Parameters.AddWithValue("realm_slug", normalizedRealm) |> ignore
    cmd.Parameters.AddWithValue("minutes", int MudChatRepository.presenceMinutes) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        let characterId = reader.GetGuid(0)
        let userId = reader.GetGuid(4)
        yield
            { CharacterId = characterId
              DisplayName = reader.GetString(1)
              PortraitUrl = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
              Title = deriveTitle userId
              CurrentRoomName = reader.GetString(3)
              IsSelf = viewerCharacterId = Some characterId } ]

let private paidSlotsForTier = function
    | Some "curious-mind" -> 3
    | Some "lab-assistant" -> 6
    | Some "research-fellow" -> 9
    | Some "professor" -> 12
    | Some "dean" -> 15
    | _ -> 0

let private loadUserSettings (conn: NpgsqlConnection) (userId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT active_mud_character_id, patreon_tier_id, COALESCE(mud_bonus_character_slots, 0)
           FROM users
           WHERE id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { ActiveCharacterId = if reader.IsDBNull(0) then None else Some (reader.GetGuid(0))
              PatreonTierId = if reader.IsDBNull(1) then None else Some (reader.GetString(1))
              BonusSlots = reader.GetInt32(2) }
    else
        None

let private characterSelectSql =
    """SELECT id,
              user_id,
              realm_slug,
              name,
              display_name,
              current_room_id,
              stat_presence,
              stat_wit,
              stat_resolve,
              stat_lore,
              stat_craft,
              stat_guile,
              skill_searching,
              skill_crafting,
              skill_navigation,
              skill_lorekeeping,
              skill_negotiation,
              skill_devices,
              skill_survival,
              created_at,
              updated_at
       FROM mud_characters
       WHERE user_id = @uid
         AND deleted_at IS NULL
       ORDER BY created_at, name"""

let private loadCharacters (conn: NpgsqlConnection) (userId: Guid) =
    use cmd = new NpgsqlCommand(characterSelectSql, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield readCharacter reader ]

let private resolveSelectedCharacter (settings: MudUserSettings option) (characters: MudCharacterRow list) =
    let byId id = characters |> List.tryFind (fun character -> character.Id = id)
    match settings |> Option.bind _.ActiveCharacterId |> Option.bind byId with
    | Some character -> Some character
    | None -> characters |> List.tryHead

let private loadStateForCharacter (conn: NpgsqlConnection) (userId: Guid) (character: MudCharacterRow) : MudRoomState option =
    use cmd = new NpgsqlCommand(
        """SELECT c.id,
                  c.display_name,
                  c.realm_slug,
                  CASE c.realm_slug
                      WHEN 'medieval' THEN 'Medieval'
                      WHEN 'sci-fi' THEN 'Sci-Fi'
                      ELSE initcap(replace(c.realm_slug, '-', ' '))
                  END,
                  c.stat_presence,
                  c.stat_wit,
                  c.stat_resolve,
                  c.stat_lore,
                  c.stat_craft,
                  c.stat_guile,
                  c.skill_searching,
                  c.skill_crafting,
                  c.skill_navigation,
                  c.skill_lorekeeping,
                  c.skill_negotiation,
                  c.skill_devices,
                  c.skill_survival,
                  r.id,
                  r.name,
                  r.description,
                  z.name,
                  c.archetype_slug,
                  c.bio,
                  c.portrait_url
           FROM mud_characters c
           JOIN mud_rooms r ON r.id = c.current_room_id
           JOIN mud_zones z ON z.id = r.zone_id
           WHERE c.id = @character_id
             AND c.user_id = @uid
             AND c.deleted_at IS NULL""", conn)
    cmd.Parameters.AddWithValue("character_id", character.Id) |> ignore
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if not (reader.Read()) then
        None
    else
        let baseState = readStateBase reader
        reader.Close()

        use exitCmd = new NpgsqlCommand(
            """SELECT e.direction, e.exit_type, e.label, e.to_room_id, r.name
               FROM mud_exits e
               JOIN mud_rooms r ON r.id = e.to_room_id
               WHERE e.from_room_id = @room_id
               ORDER BY e.direction""", conn)
        exitCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        use exitReader = exitCmd.ExecuteReader()
        let exits =
            [ while exitReader.Read() do
                yield { Direction = exitReader.GetString(0)
                        ExitType = exitReader.GetString(1)
                        Label = if exitReader.IsDBNull(2) then None else Some (exitReader.GetString(2))
                        TargetRoomId = exitReader.GetGuid(3)
                        TargetRoomName = exitReader.GetString(4) } ]
        exitReader.Close()

        use roomItemsCmd = new NpgsqlCommand(
            """SELECT name, slug, description, portable, readable_text
               FROM mud_items
               WHERE room_id = @room_id
                 AND owner_character_id IS NULL
               ORDER BY position, name""", conn)
        roomItemsCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        use roomItemsReader = roomItemsCmd.ExecuteReader()
        let visibleItems =
            [ while roomItemsReader.Read() do
                yield readItemView roomItemsReader ]
        roomItemsReader.Close()

        use invCmd = new NpgsqlCommand(
            """SELECT name, slug, description, portable, readable_text
               FROM mud_items
               WHERE owner_character_id = @character_id
               ORDER BY position, name""", conn)
        invCmd.Parameters.AddWithValue("character_id", baseState.CharacterId) |> ignore
        use invReader = invCmd.ExecuteReader()
        let inventoryItems =
            [ while invReader.Read() do
                yield readItemView invReader ]
        invReader.Close()

        use mapRoomsCmd = new NpgsqlCommand(
            """SELECT r.id, r.name, r.slug, COALESCE(r.map_x, r.position), COALESCE(r.map_y, 0),
                      (v.room_id IS NOT NULL) AS visited
               FROM mud_rooms r
               LEFT JOIN mud_character_room_visits v
                 ON v.room_id = r.id AND v.character_id = @character_id
               WHERE r.zone_id = (
                   SELECT zone_id FROM mud_rooms WHERE id = @room_id
               )
               ORDER BY r.position, r.name""", conn)
        mapRoomsCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        mapRoomsCmd.Parameters.AddWithValue("character_id", baseState.CharacterId) |> ignore
        use mapRoomsReader = mapRoomsCmd.ExecuteReader()
        let mapRooms =
            [ while mapRoomsReader.Read() do
                let roomId = mapRoomsReader.GetGuid(0)
                yield { RoomId = roomId
                        RoomName = mapRoomsReader.GetString(1)
                        Slug = mapRoomsReader.GetString(2)
                        X = mapRoomsReader.GetInt32(3)
                        Y = mapRoomsReader.GetInt32(4)
                        Current = roomId = baseState.RoomId
                        Visited = mapRoomsReader.GetBoolean(5) || roomId = baseState.RoomId } ]
        mapRoomsReader.Close()

        use mapExitsCmd = new NpgsqlCommand(
            """SELECT e.from_room_id, e.to_room_id, e.direction, e.exit_type, e.label
               FROM mud_exits e
               JOIN mud_rooms rf ON rf.id = e.from_room_id
               JOIN mud_rooms rt ON rt.id = e.to_room_id
               WHERE rf.zone_id = (
                   SELECT zone_id FROM mud_rooms WHERE id = @room_id
               )
                 AND rt.zone_id = rf.zone_id
               ORDER BY e.direction""", conn)
        mapExitsCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        use mapExitsReader = mapExitsCmd.ExecuteReader()
        let mapExits =
            [ while mapExitsReader.Read() do
                yield { FromRoomId = mapExitsReader.GetGuid(0)
                        ToRoomId = mapExitsReader.GetGuid(1)
                        Direction = mapExitsReader.GetString(2)
                        ExitType = mapExitsReader.GetString(3)
                        Label = if mapExitsReader.IsDBNull(4) then None else Some (mapExitsReader.GetString(4)) } ]
        mapExitsReader.Close()

        let mudTierName = loadMudTierName userId
        let currencyBalance = getCurrencyBalance conn baseState.CharacterId baseState.RealmSlug
        let currencyName, currencyNamePlural = currencyNames baseState.RealmSlug
        let title = deriveTitle userId
        Some
            { baseState with
                Exits = exits
                VisibleItems = visibleItems
                InventoryItems = inventoryItems
                MapRooms = mapRooms
                MapExits = mapExits
                MudTierName = mudTierName
                Title = title
                CurrencyBalance = currencyBalance
                CurrencyName = currencyName
                CurrencyNamePlural = currencyNamePlural }

let private getActiveStateInternal (conn: NpgsqlConnection) (userId: Guid) =
    let settings = loadUserSettings conn userId
    let characters = loadCharacters conn userId
    resolveSelectedCharacter settings characters
    |> Option.bind (loadStateForCharacter conn userId)

let private loadInventoryItemIds (conn: NpgsqlConnection) (characterId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT id, slug
           FROM mud_items
           WHERE owner_character_id = @character_id
           ORDER BY position, created_at, name""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield reader.GetGuid(0), reader.GetString(1).ToLowerInvariant() ]

let private realmStartRoomId (conn: NpgsqlConnection) (realmSlug: string) =
    match realmBySlug realmSlug with
    | None -> None
    | Some realm ->
        use cmd = new NpgsqlCommand(
            """SELECT id
               FROM mud_rooms
               WHERE slug = @slug
               LIMIT 1""", conn)
        cmd.Parameters.AddWithValue("slug", realm.StartRoomSlug) |> ignore
        let scalar = cmd.ExecuteScalar()
        if isNull scalar || scalar = box DBNull.Value then None else Some (scalar :?> Guid)

let private describeState (state: MudRoomState) =
    let exitsText =
        match state.Exits with
        | [] -> "No exits are visible."
        | exits ->
            exits
            |> List.map (fun e ->
                match e.Label with
                | Some label when not (String.IsNullOrWhiteSpace label) -> $"{e.Direction} ({label})"
                | _ -> e.Direction)
            |> String.concat ", "
            |> fun s -> $"Exits: {s}"

    let description =
        state.RoomDescription
        |> Option.defaultValue "The room has no description yet."

    let itemsText =
        match state.VisibleItems with
        | [] -> "Visible items: none."
        | items ->
            items
            |> List.map _.Name
            |> String.concat ", "
            |> fun s -> $"Visible items: {s}"

    $"{state.RoomName}\n\n{description}\n\n{exitsText}\n{itemsText}"

let private tryFindItem (items: MudItemView list) (query: string) =
    let normalized = query.Trim().ToLowerInvariant()
    items
    |> List.tryFind (fun item ->
        item.Name.ToLowerInvariant() = normalized
        || item.Slug.ToLowerInvariant() = normalized
        || item.Name.ToLowerInvariant().Contains(normalized))

let private describeItem (item: MudItemView) =
    let body = item.Description |> Option.defaultValue "It has no description yet."
    let portability = if item.Portable then "It looks portable." else "It looks fixed in place."
    let readable = if item.Readable then "It can be read." else "There is nothing readable on it."
    $"{item.Name}\n\n{body}\n\n{portability} {readable}"

let private loadReadableText (conn: NpgsqlConnection) (state: MudRoomState) (query: string) =
    use cmd = new NpgsqlCommand(
        """SELECT readable_text
           FROM mud_items
           WHERE readable_text IS NOT NULL
             AND (
                 room_id = @room_id
                 OR owner_character_id = @character_id
             )
             AND (
                 lower(name) = lower(@query)
                 OR lower(slug) = lower(@query)
             )
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", state.RoomId) |> ignore
    cmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
    cmd.Parameters.AddWithValue("query", query.Trim()) |> ignore
    let scalar = cmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then None else Some (scalar :?> string)

let private legacyResourceSlugs =
    [ "brass-shard"; "wire-spool"; "rag-strip"; "lamp-oil"; "wax-seal"
      "rune-chalk"; "resin-pitch"; "iron-nails"; "fiber-bundle"
      "capacitor-cell"; "coolant-canister"; "crystal-vial"
      "charcoal-stick"; "linen-cord"; "copper-clasp"; "pilgrim-token"
      "alloy-plate"; "glow-filament"; "data-shard"; "sealant-foam"
      "hemp-twine"; "flint-shard"; "tallow-cake"; "dried-herbs"
      "spring-water"; "leather-strap"; "beacon-shell"; "mag-clamp"
      "servo-core"; "nutrient-gel"; "sterile-gauze"; "cipher-tape"
      "willow-bark"; "sinew-cord"; "pitch-knot"; "moon-moss"
      "river-clay"; "oak-gall"; "tether-line"; "seal-ring"
      "nav-crystal"; "thermal-blanket"; "plasma-cell"; "ration-tin"
      "grave-iron"; "root-amber"; "thistle-silk"; "dream-wax"
      "echo-water"; "ever-tallow"; "sky-slate"; "beeswax-block"
      "beacon-coal"; "falcon-feather"; "mist-crystal"; "star-iron"
      "static-pearl"; "packet-shell"; "index-coral"; "carrier-thread"
      "phantom-code"; "silence-glass"; "void-barnacle"; "mag-bearing"
      "scar-glass"; "aerial-wire"; "vane-foil"; "lens-shard" ]

let private isResourceSlug (conn: NpgsqlConnection) (slug: string) =
    try
        if ensureCraftRecipeCatalogSeeded conn then
            use cmd = new NpgsqlCommand(
                """SELECT EXISTS(
                       SELECT 1
                       FROM mud_craft_recipe_ingredients i
                       JOIN mud_craft_recipes r ON r.id = i.recipe_id
                       WHERE r.active = TRUE
                         AND lower(i.ingredient_slug) = lower(@slug)
                   )""", conn)
            cmd.Parameters.AddWithValue("slug", slug) |> ignore
            cmd.ExecuteScalar() :?> bool
        else
            legacyResourceSlugs |> List.contains slug
    with _ ->
        legacyResourceSlugs |> List.contains slug

let private awardAchievementBySlug (userId: Guid) (slug: string) =
    match AchievementRepository.getAchievementBySlug slug with
    | Some achievement -> AchievementRepository.awardAchievement userId achievement.Id |> ignore
    | None -> ()

let private describeRecipe (recipe: MudCraftRecipeDefinition) =
    let ingredients =
        recipe.Ingredients
        |> List.map (fun ingredient ->
            let quantityPrefix = if ingredient.Quantity > 1 then $"{ingredient.Quantity}x " else ""
            let ingredientText = ingredient.Slug.Replace("-", " ")
            quantityPrefix + ingredientText)
        |> String.concat ", "
    $"{recipe.Name}\n\nIngredients: {ingredients}\nCreates: {recipe.OutputName}"

let private describeRecipes (conn: NpgsqlConnection) =
    loadCraftRecipes conn
    |> List.map describeRecipe
    |> String.concat "\n\n"

let private tryResolveRecipe (conn: NpgsqlConnection) (query: string) =
    let normalized = query.Trim().ToLowerInvariant()
    loadCraftRecipes conn
    |> List.tryFind (fun recipe ->
        recipe.Slug = normalized
        || recipe.Name.ToLowerInvariant() = normalized
        || recipe.Name.ToLowerInvariant().Replace(" ", "-") = normalized)

let private logEvent
    (conn: NpgsqlConnection)
    (actorUserId: Guid)
    (actorCharacterId: Guid)
    (roomId: Guid)
    (eventType: string)
    (command: string option)
    (message: string)
    (payload: string) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_events (actor_type, actor_user_id, actor_character_id, room_id, event_type, command, message, payload)
           VALUES ('user', @actor_user_id, @actor_character_id, @room_id, @event_type, @command, @message, @payload::jsonb)""", conn)
    cmd.Parameters.AddWithValue("actor_user_id", actorUserId) |> ignore
    cmd.Parameters.AddWithValue("actor_character_id", actorCharacterId) |> ignore
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.Parameters.AddWithValue("event_type", eventType) |> ignore
    cmd.Parameters.AddWithValue("command", command |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("message", message) |> ignore
    cmd.Parameters.AddWithValue("payload", payload) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private recordRoomVisit (conn: NpgsqlConnection) (characterId: Guid) (roomId: Guid) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_character_room_visits (character_id, room_id)
           VALUES (@character_id, @room_id)
           ON CONFLICT DO NOTHING""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Fogs the zone map down to rooms the character has actually visited.
/// Admins see the full zone map unfiltered.
let applyMapVisibility (isAdmin: bool) (state: MudRoomState) : MudRoomState =
    if isAdmin then
        state
    else
        let visitedIds = state.MapRooms |> List.filter _.Visited |> List.map _.RoomId |> Set.ofList
        { state with
            MapRooms = state.MapRooms |> List.filter _.Visited
            MapExits = state.MapExits |> List.filter (fun exit -> visitedIds.Contains exit.FromRoomId && visitedIds.Contains exit.ToRoomId) }

let private withState (userId: Guid) (action: MudRoomState -> MudCommandResult) : MudCommandResult =
    use conn = openConnection ()
    match getActiveStateInternal conn userId with
    | None ->
        { Success = false
          Command = ""
          Message = "Choose or create a character first."
          State = None }
    | Some state ->
        action state

let private findExit (conn: NpgsqlConnection) (roomId: Guid) (direction: string) =
    use cmd = new NpgsqlCommand(
        """SELECT e.direction, e.exit_type, e.label, e.to_room_id, r.name
           FROM mud_exits e
           JOIN mud_rooms r ON r.id = e.to_room_id
           WHERE e.from_room_id = @room_id
             AND lower(e.direction) = lower(@direction)
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.Parameters.AddWithValue("direction", direction) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { Direction = reader.GetString(0)
              ExitType = reader.GetString(1)
              Label = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
              TargetRoomId = reader.GetGuid(3)
              TargetRoomName = reader.GetString(4) }
    else
        None

let private paidSlotsUsed (characters: MudCharacterRow list) =
    let freeRealmCount =
        realmDefinitions
        |> List.sumBy (fun realm ->
            if characters |> List.exists (fun character -> character.RealmSlug = realm.Slug) then 1 else 0)
    max 0 (characters.Length - freeRealmCount)

let private buildRoster (userId: Guid) (settings: MudUserSettings option) (characters: MudCharacterRow list) =
    let selectedCharacter = resolveSelectedCharacter settings characters
    let selectedCharacterId = selectedCharacter |> Option.map _.Id
    let mudTierName = loadMudTierName userId
    let bonusSlots = settings |> Option.map _.BonusSlots |> Option.defaultValue 0
    let paidSlotsTotal = (settings |> Option.bind _.PatreonTierId |> paidSlotsForTier |> max 0) + bonusSlots
    let paidUsed = paidSlotsUsed characters
    let remaining = max 0 (paidSlotsTotal - paidUsed)

    let characterSummaries =
        use conn = openConnection ()
        use cmd = new NpgsqlCommand(
            """SELECT c.id,
                      c.user_id,
                      c.realm_slug,
                      c.name,
                      c.display_name,
                      c.current_room_id,
                      c.stat_presence,
                      c.stat_wit,
                      c.stat_resolve,
                      c.stat_lore,
                      c.stat_craft,
                      c.stat_guile,
                      c.skill_searching,
                      c.skill_crafting,
                      c.skill_navigation,
                      c.skill_lorekeeping,
                      c.skill_negotiation,
                      c.skill_devices,
                      c.skill_survival,
                      c.created_at,
                      c.updated_at,
                      r.name,
                      COALESCE((
                          SELECT COUNT(*)
                          FROM mud_items i
                          WHERE i.owner_character_id = c.id
                      ), 0),
                      c.archetype_slug,
                      c.bio,
                      c.portrait_url
               FROM mud_characters c
               JOIN mud_rooms r ON r.id = c.current_room_id
               WHERE c.user_id = @uid
                 AND c.deleted_at IS NULL
               ORDER BY c.created_at, c.name""", conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        use reader = cmd.ExecuteReader()
        let title = deriveTitle userId
        [ while reader.Read() do
            let character = readCharacter reader
            yield
                { Id = character.Id
                  Name = character.Name
                  DisplayName = character.DisplayName
                  RealmSlug = character.RealmSlug
                  RealmName = realmName character.RealmSlug
                  IsSelected = selectedCharacterId = Some character.Id
                  CurrentRoomName = reader.GetString(21)
                  MudTierName = mudTierName
                  InventoryCount = reader.GetInt32(22)
                  Stats = character.Stats
                  Skills = character.Skills
                  ArchetypeSlug = if reader.IsDBNull(23) then None else Some (reader.GetString(23))
                  Bio = if reader.IsDBNull(24) then None else Some (reader.GetString(24))
                  PortraitUrl = if reader.IsDBNull(25) then None else Some (reader.GetString(25))
                  Title = title
                  CreatedAt = character.CreatedAt } ]

    let realms =
        realmDefinitions
        |> List.map (fun realm ->
            let count = characters |> List.filter (fun character -> character.RealmSlug = realm.Slug) |> List.length
            { RealmSlug = realm.Slug
              RealmName = realm.Name
              CharacterCount = count
              FreeStarterUsed = count > 0
              CanCreateFreeStarter = count = 0 })

    { SelectedCharacterId = selectedCharacterId
      Characters = characterSummaries
      Realms = realms
      PaidSlotsTotal = paidSlotsTotal
      PaidSlotsUsed = paidUsed
      PaidSlotsRemaining = remaining
      BonusSlots = bonusSlots }

let getRoster (userId: Guid) =
    use conn = openConnection ()
    let settings = loadUserSettings conn userId
    let characters = loadCharacters conn userId
    buildRoster userId settings characters

let private realmCountsByRealmSlug (conn: NpgsqlConnection) =
    use cmd = new NpgsqlCommand(
        """SELECT z.realm_slug,
                  COUNT(DISTINCT z.id)::int AS zone_count,
                  COUNT(r.id)::int AS room_count
           FROM mud_zones z
           LEFT JOIN mud_rooms r ON r.zone_id = z.id
           GROUP BY z.realm_slug""", conn)
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield reader.GetString(0), (reader.GetInt32(1), reader.GetInt32(2)) ]
    |> Map.ofList

let getLandingStats () =
    use conn = openConnection ()
    let recipeCount =
        try
            if ensureCraftRecipeCatalogSeeded conn then
                use recipeCmd = new NpgsqlCommand("SELECT COUNT(*)::int FROM mud_craft_recipes WHERE active = TRUE", conn)
                recipeCmd.ExecuteScalar() :?> int
            else
                craftRecipes.Length
        with _ ->
            craftRecipes.Length
    let realmCounts =
        try realmCountsByRealmSlug conn
        with _ -> Map.empty
    let realms =
        realmDefinitions
        |> List.map (fun realm ->
            let zoneCount, roomCount = realmCounts |> Map.tryFind realm.Slug |> Option.defaultValue (0, 0)
            { Slug = realm.Slug
              Name = realm.Name
              Description = realm.Description
              ZoneCount = zoneCount
              RoomCount = roomCount })
    use cmd = new NpgsqlCommand(
        """SELECT
               (SELECT COUNT(*)::int FROM mud_rooms),
               (SELECT COUNT(*)::int FROM mud_zones)""", conn)
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        { RoomCount = reader.GetInt32(0)
          ZoneCount = reader.GetInt32(1)
          RecipeCount = recipeCount
          Realms = realms }
    else
        { RoomCount = 0
          ZoneCount = 0
          RecipeCount = recipeCount
          Realms = realms }

let getState (userId: Guid) : MudRoomState option =
    use conn = openConnection ()
    getActiveStateInternal conn userId

let selectCharacter (userId: Guid) (characterId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE users
           SET active_mud_character_id = @character_id
           WHERE id = @uid
             AND EXISTS (
                SELECT 1
                FROM mud_characters c
                WHERE c.id = @character_id
                  AND c.user_id = @uid
                  AND c.deleted_at IS NULL
             )""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    if cmd.ExecuteNonQuery() > 0 then getState userId else None

let private createCharacterAllowed (realmSlug: string) (roster: MudRosterView) =
    match roster.Realms |> List.tryFind (fun realm -> realm.RealmSlug = realmSlug) with
    | None -> false, "Unknown realm."
    | Some realm when realm.CanCreateFreeStarter -> true, ""
    | Some _ when roster.PaidSlotsRemaining > 0 -> true, ""
    | Some _ -> false, "Your roster is full. Delete a character, upgrade your tier, or add another slot."

let createCharacter
    (userId: Guid)
    (realmSlug: string)
    (name: string)
    (displayName: string option)
    (archetypeSlug: string)
    (allocation: MudStats)
    (bio: string option) =
    let trimmedName = name.Trim()
    if String.IsNullOrWhiteSpace(trimmedName) then
        Error "Character name is required."
    else
        let normalizedRealm = normalizeRealmSlug realmSlug
        match realmBySlug normalizedRealm with
        | None -> Error "Unknown realm."
        | Some realm ->
        match archetypeBySlug normalizedRealm archetypeSlug with
        | None -> Error "Choose a background for your character."
        | Some archetype ->
        match pendingStatRolls.TryGetValue(userId) with
        | false, _ -> Error "Roll your stats before creating a character."
        | true, roll ->
        match validateStatAllocation archetype roll allocation with
        | Error message -> Error message
        | Ok stats ->
            use conn = openConnection ()
            let settings = loadUserSettings conn userId
            let characters = loadCharacters conn userId
            let roster = buildRoster userId settings characters
            let allowed, message = createCharacterAllowed normalizedRealm roster
            if not allowed then
                Error message
            else
                match realmStartRoomId conn normalizedRealm with
                | None -> Error "That realm does not have a start room yet."
                | Some roomId ->
                    let chosenDisplayName =
                        displayName
                        |> nonBlank
                        |> Option.defaultValue trimmedName
                    let trimmedBio = bio |> Option.map (fun b -> b.Trim()) |> Option.filter (String.IsNullOrWhiteSpace >> not)
                    use cmd = new NpgsqlCommand(
                        """INSERT INTO mud_characters (
                               user_id,
                               realm_slug,
                               name,
                               display_name,
                               current_room_id,
                               archetype_slug,
                               bio,
                               stat_presence,
                               stat_wit,
                               stat_resolve,
                               stat_lore,
                               stat_craft,
                               stat_guile,
                               skill_searching,
                               skill_crafting,
                               skill_navigation,
                               skill_lorekeeping,
                               skill_negotiation,
                               skill_devices,
                               skill_survival
                           )
                           VALUES (
                               @uid,
                               @realm_slug,
                               @name,
                               @display_name,
                               @room_id,
                               @archetype_slug,
                               @bio,
                               @stat_presence,
                               @stat_wit,
                               @stat_resolve,
                               @stat_lore,
                               @stat_craft,
                               @stat_guile,
                               @skill_searching,
                               @skill_crafting,
                               @skill_navigation,
                               @skill_lorekeeping,
                               @skill_negotiation,
                               @skill_devices,
                               @skill_survival
                           )
                           RETURNING id""", conn)
                    cmd.Parameters.AddWithValue("uid", userId) |> ignore
                    cmd.Parameters.AddWithValue("realm_slug", realm.Slug) |> ignore
                    cmd.Parameters.AddWithValue("name", trimmedName) |> ignore
                    cmd.Parameters.AddWithValue("display_name", chosenDisplayName) |> ignore
                    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
                    cmd.Parameters.AddWithValue("archetype_slug", archetype.Slug) |> ignore
                    cmd.Parameters.AddWithValue("bio", trimmedBio |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                    cmd.Parameters.AddWithValue("stat_presence", stats.Presence) |> ignore
                    cmd.Parameters.AddWithValue("stat_wit", stats.Wit) |> ignore
                    cmd.Parameters.AddWithValue("stat_resolve", stats.Resolve) |> ignore
                    cmd.Parameters.AddWithValue("stat_lore", stats.Lore) |> ignore
                    cmd.Parameters.AddWithValue("stat_craft", stats.Craft) |> ignore
                    cmd.Parameters.AddWithValue("stat_guile", stats.Guile) |> ignore
                    cmd.Parameters.AddWithValue("skill_searching", defaultSkills.Searching) |> ignore
                    cmd.Parameters.AddWithValue("skill_crafting", defaultSkills.Crafting) |> ignore
                    cmd.Parameters.AddWithValue("skill_navigation", defaultSkills.Navigation) |> ignore
                    cmd.Parameters.AddWithValue("skill_lorekeeping", defaultSkills.Lorekeeping) |> ignore
                    cmd.Parameters.AddWithValue("skill_negotiation", defaultSkills.Negotiation) |> ignore
                    cmd.Parameters.AddWithValue("skill_devices", defaultSkills.Devices) |> ignore
                    cmd.Parameters.AddWithValue("skill_survival", defaultSkills.Survival) |> ignore
                    let insertedId = cmd.ExecuteScalar()
                    if isNull insertedId || insertedId = box DBNull.Value then
                        Error "Failed to create character."
                    else
                        let characterId = insertedId :?> Guid
                        use selectCmd = new NpgsqlCommand(
                            """UPDATE users
                               SET active_mud_character_id = @character_id
                               WHERE id = @uid""", conn)
                        selectCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
                        selectCmd.Parameters.AddWithValue("uid", userId) |> ignore
                        selectCmd.ExecuteNonQuery() |> ignore
                        recordRoomVisit conn characterId roomId
                        adjustCurrency conn characterId realm.Slug StarterCurrencyGrant |> ignore

                        use starterItemCmd = new NpgsqlCommand(
                            """INSERT INTO mud_items (owner_character_id, name, slug, description, portable, position)
                               VALUES (@character_id, @name, @slug, @description, true, 0)""", conn)
                        starterItemCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
                        starterItemCmd.Parameters.AddWithValue("name", archetype.StarterItemName) |> ignore
                        starterItemCmd.Parameters.AddWithValue("slug", archetype.StarterItemSlug) |> ignore
                        starterItemCmd.Parameters.AddWithValue("description", archetype.StarterItemDescription) |> ignore
                        starterItemCmd.ExecuteNonQuery() |> ignore

                        pendingStatRolls.TryRemove(userId) |> ignore
                        Ok (getRoster userId)

let deleteCharacter (userId: Guid) (characterId: Guid) =
    use conn = openConnection ()
    use existsCmd = new NpgsqlCommand(
        """SELECT current_room_id
           FROM mud_characters
           WHERE id = @character_id
             AND user_id = @uid
             AND deleted_at IS NULL""", conn)
    existsCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    existsCmd.Parameters.AddWithValue("uid", userId) |> ignore
    let scalar = existsCmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then
        false
    else
        let currentRoomId = scalar :?> Guid
        use clearItemsCmd = new NpgsqlCommand("DELETE FROM mud_items WHERE owner_character_id = @character_id", conn)
        clearItemsCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        clearItemsCmd.ExecuteNonQuery() |> ignore

        use deactivateCmd = new NpgsqlCommand(
            """UPDATE users
               SET active_mud_character_id = NULL
               WHERE id = @uid
                 AND active_mud_character_id = @character_id""", conn)
        deactivateCmd.Parameters.AddWithValue("uid", userId) |> ignore
        deactivateCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        deactivateCmd.ExecuteNonQuery() |> ignore

        use softDeleteCmd = new NpgsqlCommand(
            """UPDATE mud_characters
               SET deleted_at = now(),
                   updated_at = now()
               WHERE id = @character_id
                 AND user_id = @uid
                 AND deleted_at IS NULL""", conn)
        softDeleteCmd.Parameters.AddWithValue("character_id", characterId) |> ignore
        softDeleteCmd.Parameters.AddWithValue("uid", userId) |> ignore
        let deleted = softDeleteCmd.ExecuteNonQuery() > 0

        if deleted then
            let characters = loadCharacters conn userId
            let nextActive = characters |> List.tryHead
            match nextActive with
            | Some nextCharacter ->
                use chooseNextCmd = new NpgsqlCommand(
                    """UPDATE users
                       SET active_mud_character_id = @character_id
                       WHERE id = @uid
                         AND active_mud_character_id IS NULL""", conn)
                chooseNextCmd.Parameters.AddWithValue("character_id", nextCharacter.Id) |> ignore
                chooseNextCmd.Parameters.AddWithValue("uid", userId) |> ignore
                chooseNextCmd.ExecuteNonQuery() |> ignore
            | None -> ()

            logEvent conn userId characterId currentRoomId "character-delete" None "Character deleted." (payloadOf [ "character_id", string characterId ])

        deleted

let updateCharacterPortrait (userId: Guid) (characterId: Guid) (portraitUrl: string) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_characters
           SET portrait_url = @url,
               updated_at = now()
           WHERE id = @character_id
             AND user_id = @uid
             AND deleted_at IS NULL""", conn)
    cmd.Parameters.AddWithValue("url", portraitUrl) |> ignore
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.ExecuteNonQuery() > 0

let updateCharacterBio (userId: Guid) (characterId: Guid) (bio: string option) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_characters
           SET bio = @bio,
               updated_at = now()
           WHERE id = @character_id
             AND user_id = @uid
             AND deleted_at IS NULL""", conn)
    cmd.Parameters.AddWithValue("bio", bio |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.ExecuteNonQuery() > 0

let look (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        let message = describeState state
        use conn = openConnection ()
        logEvent conn userId state.CharacterId state.RoomId "look" (Some "look") message (payloadOf [ "action", "look" ])
        { Success = true
          Command = "look"
          Message = message
          State = Some state })

let private moveInternal (userId: Guid) (direction: string) : MudCommandResult =
    use conn = openConnection ()
    match getActiveStateInternal conn userId with
    | None ->
        { Success = false
          Command = $"move {direction}"
          Message = "Choose or create a character first."
          State = None }
    | Some state ->
        match findExit conn state.RoomId direction with
        | None ->
            logEvent conn userId state.CharacterId state.RoomId "move-failed" (Some direction) $"No exit in direction '{direction}'." (payloadOf [ "direction", direction ])
            { Success = false
              Command = $"move {direction}"
              Message = $"There is no exit to the {direction}."
              State = Some state }
        | Some exitView ->
            use moveCmd = new NpgsqlCommand(
                """UPDATE mud_characters
                   SET current_room_id = @room_id,
                       updated_at = now()
                   WHERE id = @id""", conn)
            moveCmd.Parameters.AddWithValue("room_id", exitView.TargetRoomId) |> ignore
            moveCmd.Parameters.AddWithValue("id", state.CharacterId) |> ignore
            moveCmd.ExecuteNonQuery() |> ignore
            recordRoomVisit conn state.CharacterId exitView.TargetRoomId
            let nextState =
                match getActiveStateInternal conn userId with
                | Some s -> s
                | None -> state
            if state.RealmSlug <> nextState.RealmSlug then
                awardAchievementBySlug userId "mud-realmwalker"
            match nextState.RoomName with
            | "Reliquary" | "Warden Vault" -> awardAchievementBySlug userId "mud-vault-delver"
            | "Reactor Causeway" | "Signal Apex" -> awardAchievementBySlug userId "mud-star-runner"
            | _ -> ()
            logEvent conn userId state.CharacterId nextState.RoomId "move" (Some direction) $"Moved to {nextState.RoomName}." (payloadOf [ "direction", direction; "to_room", exitView.TargetRoomName ])
            { Success = true
              Command = $"move {direction}"
              Message = $"You move {direction} to {exitView.TargetRoomName}."
              State = Some nextState }

let move (userId: Guid) (direction: string) =
    moveInternal userId direction

let examine (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "examine"
              Message = "Examine what?"
              State = Some state }
        else
            let lower = trimmed.ToLowerInvariant()
            let roomMatch =
                lower = "room"
                || lower = "here"
                || lower = state.RoomName.ToLowerInvariant()
            let item =
                tryFindItem state.VisibleItems trimmed
                |> Option.orElseWith (fun () -> tryFindItem state.InventoryItems trimmed)
            let exitView =
                state.Exits
                |> List.tryFind (fun exit ->
                    exit.Direction.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                    || exit.TargetRoomName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)

            let message =
                if roomMatch then
                    describeState state
                else
                    match item, exitView with
                    | Some foundItem, _ -> describeItem foundItem
                    | None, Some exit -> $"Exit {exit.Direction}\n\nIt leads toward {exit.TargetRoomName}."
                    | None, None when lower = "self" || lower = "me" ->
                        $"{state.CharacterName}\n\nRealm: {state.RealmName}\nRank: {state.MudTierName}\nPresence: {state.Stats.Presence}\nWit: {state.Stats.Wit}\nResolve: {state.Stats.Resolve}"
                    | None, None -> $"You do not see '{trimmed}' here."

            use conn = openConnection ()
            logEvent conn userId state.CharacterId state.RoomId "examine" (Some trimmed) message (payloadOf [ "target", trimmed ])
            { Success = item.IsSome || exitView.IsSome || roomMatch || lower = "self" || lower = "me"
              Command = $"examine {trimmed}"
              Message = message
              State = Some state })

let read (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "read"
              Message = "Read what?"
              State = Some state }
        else
            use conn = openConnection ()
            let message =
                match loadReadableText conn state trimmed with
                | Some text ->
                    awardAchievementBySlug userId "mud-lorekeeper"
                    text
                | None -> $"There is nothing readable on '{trimmed}'."
            logEvent conn userId state.CharacterId state.RoomId "read" (Some trimmed) message (payloadOf [ "target", trimmed ])
            { Success = message <> $"There is nothing readable on '{trimmed}'."
              Command = $"read {trimmed}"
              Message = message
              State = Some state })

let talk (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed =
            query.Trim()
            |> fun value ->
                if value.StartsWith("to ", StringComparison.OrdinalIgnoreCase) then value.Substring(3).Trim()
                else value

        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "talk"
              Message = "Talk to whom?"
              State = Some state }
        else
            use conn = openConnection ()
            let message =
                match loadReadableText conn state trimmed with
                | Some text ->
                    awardAchievementBySlug userId "mud-speaks-first"
                    text
                | None -> $"'{trimmed}' offers no reply."
            logEvent conn userId state.CharacterId state.RoomId "talk" (Some trimmed) message (payloadOf [ "target", trimmed ])
            { Success = not (message.EndsWith("offers no reply."))
              Command = $"talk {trimmed}"
              Message = message
              State = Some state })

let inventory (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        let message =
            match state.InventoryItems with
            | [] -> "You are carrying nothing."
            | items ->
                items
                |> List.map _.Name
                |> String.concat ", "
                |> fun s -> $"You are carrying: {s}"
        use conn = openConnection ()
        logEvent conn userId state.CharacterId state.RoomId "inventory" (Some "inventory") message (payloadOf [ "count", string state.InventoryItems.Length ])
        { Success = true
          Command = "inventory"
          Message = message
          State = Some state })

let search (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        use conn = openConnection ()
        let resources =
            state.VisibleItems
            |> List.filter (fun item -> item.Portable || isResourceSlug conn item.Slug)
            |> List.map _.Name
        let message =
            match resources with
            | [] -> $"{describeState state}\n\nYou do not spot any loose materials worth taking."
            | items ->
                let found = items |> String.concat ", "
                awardAchievementBySlug userId "mud-scrounger"
                $"{describeState state}\n\nSearch turns up useful materials: {found}."
        logEvent conn userId state.CharacterId state.RoomId "search" (Some "search") message (payloadOf [ "count", string resources.Length ])
        { Success = true
          Command = "search"
          Message = message
          State = Some state })

let recipes (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        use conn = openConnection ()
        let recipes = loadCraftRecipes conn
        let message = recipes |> List.map describeRecipe |> String.concat "\n\n"
        logEvent conn userId state.CharacterId state.RoomId "recipes" (Some "recipes") message (payloadOf [ "count", string recipes.Length ])
        { Success = true
          Command = "recipes"
          Message = message
          State = Some state })

let craft (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "craft"
              Message = "Craft what? Try recipes."
              State = Some state }
        else
            use conn = openConnection ()
            match tryResolveRecipe conn trimmed with
            | None ->
                { Success = false
                  Command = $"craft {trimmed}"
                  Message = $"Unknown recipe '{trimmed}'. Try recipes."
                  State = Some state }
            | Some recipe ->
                let inventoryRows = loadInventoryItemIds conn state.CharacterId
                let mutable available = inventoryRows
                let mutable missing = []
                let mutable consumeIds : Guid list = []

                for ingredient in recipe.Ingredients do
                    for _ in 1 .. ingredient.Quantity do
                        match available |> List.tryFind (fun (_, slug) -> slug = ingredient.Slug) with
                        | Some (id, _) ->
                            consumeIds <- id :: consumeIds
                            available <- available |> List.filter (fun (rowId, _) -> rowId <> id)
                        | None ->
                            missing <- ingredient.Slug :: missing

                if not missing.IsEmpty then
                    let missingText =
                        missing
                        |> List.rev
                        |> List.map (fun slug -> slug.Replace("-", " "))
                        |> String.concat ", "
                    let message = $"You are missing: {missingText}."
                    logEvent conn userId state.CharacterId state.RoomId "craft-failed" (Some trimmed) message (payloadOf [ "recipe", recipe.Slug ])
                    { Success = false
                      Command = $"craft {trimmed}"
                      Message = message
                      State = Some state }
                else
                    for consumeId in consumeIds do
                        use deleteCmd = new NpgsqlCommand("DELETE FROM mud_items WHERE id = @id", conn)
                        deleteCmd.Parameters.AddWithValue("id", consumeId) |> ignore
                        deleteCmd.ExecuteNonQuery() |> ignore

                    use insertCmd = new NpgsqlCommand(
                        """INSERT INTO mud_items (owner_character_id, name, slug, description, readable_text, portable, position)
                           VALUES (@character_id, @name, @slug, @description, @readable_text, true, 0)""", conn)
                    insertCmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                    insertCmd.Parameters.AddWithValue("name", recipe.OutputName) |> ignore
                    insertCmd.Parameters.AddWithValue("slug", recipe.OutputSlug) |> ignore
                    insertCmd.Parameters.AddWithValue("description", recipe.OutputDescription) |> ignore
                    insertCmd.Parameters.AddWithValue("readable_text", recipe.OutputReadableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                    insertCmd.ExecuteNonQuery() |> ignore

                    let nextState = getState userId |> Option.defaultValue state
                    let message = $"You craft {recipe.OutputName}."
                    awardAchievementBySlug userId "mud-crafter"
                    match recipe.OutputSlug with
                    | "torch" -> awardAchievementBySlug userId "mud-torchbearer"
                    | "signal-key" | "patch-cable" | "relay-lantern" -> awardAchievementBySlug userId "mud-signal-smith"
                    | "ward-lantern" | "star-chart" | "hull-patch" -> awardAchievementBySlug userId "mud-master-tinker"
                    | _ -> ()
                    logEvent conn userId state.CharacterId state.RoomId "craft" (Some trimmed) message (payloadOf [ "recipe", recipe.Slug; "created", recipe.OutputSlug ])
                    { Success = true
                      Command = $"craft {trimmed}"
                      Message = message
                      State = Some nextState })

// Economy: vendors, currency, and player-to-player trading

type private MudVendorRow =
    { VendorId: Guid
      Name: string
      Greeting: string option }

type private MudVendorListingRow =
    { ItemName: string
      ItemSlug: string
      ItemDescription: string option
      ItemReadableText: string option
      Portable: bool
      BuyPrice: int option
      SellPrice: int option }

let private tryFindVendorInRoom (conn: NpgsqlConnection) (roomId: Guid) : MudVendorRow option =
    use cmd = new NpgsqlCommand(
        """SELECT id, name, greeting FROM mud_vendors
           WHERE room_id = @room_id AND active = TRUE
           ORDER BY created_at LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { VendorId = reader.GetGuid(0)
              Name = reader.GetString(1)
              Greeting = if reader.IsDBNull(2) then None else Some (reader.GetString(2)) }
    else
        None

let private getVendorListings (conn: NpgsqlConnection) (vendorId: Guid) : MudVendorListingRow list =
    use cmd = new NpgsqlCommand(
        """SELECT item_name, item_slug, item_description, item_readable_text, portable, buy_price, sell_price
           FROM mud_vendor_listings
           WHERE vendor_id = @vendor_id AND active = TRUE
           ORDER BY position, item_name""", conn)
    cmd.Parameters.AddWithValue("vendor_id", vendorId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield
            { ItemName = reader.GetString(0)
              ItemSlug = reader.GetString(1)
              ItemDescription = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
              ItemReadableText = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
              Portable = reader.GetBoolean(4)
              BuyPrice = if reader.IsDBNull(5) then None else Some (reader.GetInt32(5))
              SellPrice = if reader.IsDBNull(6) then None else Some (reader.GetInt32(6)) } ]

let private tryFindListing (query: string) (listings: MudVendorListingRow list) =
    let normalized = query.Trim().ToLowerInvariant()
    listings
    |> List.tryFind (fun listing ->
        listing.ItemName.ToLowerInvariant() = normalized
        || listing.ItemSlug.ToLowerInvariant() = normalized
        || listing.ItemName.ToLowerInvariant().Contains(normalized))

let shop (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        use conn = openConnection ()
        match tryFindVendorInRoom conn state.RoomId with
        | None ->
            { Success = false
              Command = "shop"
              Message = "There is no vendor here."
              State = Some state }
        | Some vendor ->
            let listings = getVendorListings conn vendor.VendorId
            let lines =
                listings
                |> List.map (fun listing ->
                    match listing.BuyPrice, listing.SellPrice with
                    | Some buy, Some sell -> $"{listing.ItemName} (buy {buy}, sell {sell} {state.CurrencyNamePlural})"
                    | Some buy, None -> $"{listing.ItemName} (buy {buy} {state.CurrencyNamePlural})"
                    | None, Some sell -> $"{listing.ItemName} (sell {sell} {state.CurrencyNamePlural})"
                    | None, None -> listing.ItemName)
            let greeting = vendor.Greeting |> Option.defaultValue $"{vendor.Name} looks you over."
            let joinedLines = String.concat "; " lines
            let message =
                if listings.IsEmpty then $"{greeting} There is nothing for sale right now."
                else $"{greeting} Wares: {joinedLines}."
            { Success = true
              Command = "shop"
              Message = message
              State = Some state })

let buyItem (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "buy"
              Message = "Buy what?"
              State = Some state }
        else
            use conn = openConnection ()
            match tryFindVendorInRoom conn state.RoomId with
            | None ->
                { Success = false
                  Command = $"buy {trimmed}"
                  Message = "There is no vendor here."
                  State = Some state }
            | Some vendor ->
                let listings = getVendorListings conn vendor.VendorId
                match tryFindListing trimmed listings with
                | None ->
                    { Success = false
                      Command = $"buy {trimmed}"
                      Message = $"{vendor.Name} does not sell '{trimmed}'."
                      State = Some state }
                | Some listing ->
                    match listing.BuyPrice with
                    | None ->
                        { Success = false
                          Command = $"buy {trimmed}"
                          Message = $"{vendor.Name} will not sell {listing.ItemName}."
                          State = Some state }
                    | Some price ->
                        match adjustCurrency conn state.CharacterId state.RealmSlug (-price) with
                        | Error _ ->
                            { Success = false
                              Command = $"buy {trimmed}"
                              Message = $"You do not have {price} {state.CurrencyNamePlural} for {listing.ItemName}."
                              State = Some state }
                        | Ok _ ->
                            use insertCmd = new NpgsqlCommand(
                                """INSERT INTO mud_items (owner_character_id, name, slug, description, readable_text, portable, position)
                                   VALUES (@character_id, @name, @slug, @description, @readable_text, @portable, 0)""", conn)
                            insertCmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                            insertCmd.Parameters.AddWithValue("name", listing.ItemName) |> ignore
                            insertCmd.Parameters.AddWithValue("slug", listing.ItemSlug) |> ignore
                            insertCmd.Parameters.AddWithValue("description", listing.ItemDescription |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                            insertCmd.Parameters.AddWithValue("readable_text", listing.ItemReadableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                            insertCmd.Parameters.AddWithValue("portable", listing.Portable) |> ignore
                            insertCmd.ExecuteNonQuery() |> ignore
                            let message = $"You buy {listing.ItemName} for {price} {state.CurrencyNamePlural}."
                            logEvent conn userId state.CharacterId state.RoomId "buy" (Some trimmed) message (payloadOf [ "item", listing.ItemSlug; "price", string price ])
                            let nextState = getState userId |> Option.defaultValue state
                            { Success = true
                              Command = $"buy {trimmed}"
                              Message = message
                              State = Some nextState })

let sellItem (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "sell"
              Message = "Sell what?"
              State = Some state }
        else
            use conn = openConnection ()
            match tryFindVendorInRoom conn state.RoomId with
            | None ->
                { Success = false
                  Command = $"sell {trimmed}"
                  Message = "There is no vendor here."
                  State = Some state }
            | Some vendor ->
                let listings = getVendorListings conn vendor.VendorId
                match tryFindListing trimmed listings with
                | Some listing when listing.SellPrice.IsSome ->
                    use itemCmd = new NpgsqlCommand(
                        """SELECT id FROM mud_items
                           WHERE owner_character_id = @character_id AND slug = @slug
                           ORDER BY position LIMIT 1""", conn)
                    itemCmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                    itemCmd.Parameters.AddWithValue("slug", listing.ItemSlug) |> ignore
                    match itemCmd.ExecuteScalar() with
                    | null ->
                        { Success = false
                          Command = $"sell {trimmed}"
                          Message = $"You are not carrying {listing.ItemName}."
                          State = Some state }
                    | itemIdBoxed ->
                        let itemId = itemIdBoxed :?> Guid
                        let price = listing.SellPrice.Value
                        use deleteCmd = new NpgsqlCommand("DELETE FROM mud_items WHERE id = @id", conn)
                        deleteCmd.Parameters.AddWithValue("id", itemId) |> ignore
                        deleteCmd.ExecuteNonQuery() |> ignore
                        adjustCurrency conn state.CharacterId state.RealmSlug price |> ignore
                        let message = $"You sell {listing.ItemName} for {price} {state.CurrencyNamePlural}."
                        logEvent conn userId state.CharacterId state.RoomId "sell" (Some trimmed) message (payloadOf [ "item", listing.ItemSlug; "price", string price ])
                        let nextState = getState userId |> Option.defaultValue state
                        { Success = true
                          Command = $"sell {trimmed}"
                          Message = message
                          State = Some nextState }
                | _ ->
                    { Success = false
                      Command = $"sell {trimmed}"
                      Message = $"{vendor.Name} is not buying '{trimmed}'."
                      State = Some state })

let checkBalance (userId: Guid) : MudCommandResult =
    withState userId (fun state ->
        { Success = true
          Command = "balance"
          Message = $"You are carrying {state.CurrencyBalance} {state.CurrencyNamePlural}."
          State = Some state })

let private tryFindCharacterInRoom (conn: NpgsqlConnection) (roomId: Guid) (selfCharacterId: Guid) (name: string) =
    use cmd = new NpgsqlCommand(
        """SELECT id, display_name FROM mud_characters
           WHERE current_room_id = @room_id
             AND deleted_at IS NULL
             AND id <> @self
             AND lower(display_name) = lower(@name)
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.Parameters.AddWithValue("self", selfCharacterId) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (reader.GetGuid(0), reader.GetString(1)) else None

let give (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let parts = query.Trim().Split([| ' ' |], 3, StringSplitOptions.RemoveEmptyEntries)
        if parts.Length < 2 then
            { Success = false
              Command = "give"
              Message = "Give what to whom? Try: give <character> <amount> or give <character> <item>."
              State = Some state }
        else
            use conn = openConnection ()
            let targetName = parts.[0]
            let rest = parts.[1]
            match tryFindCharacterInRoom conn state.RoomId state.CharacterId targetName with
            | None ->
                { Success = false
                  Command = "give"
                  Message = $"There is no one named '{targetName}' here."
                  State = Some state }
            | Some (targetId, targetDisplayName) ->
                match Int32.TryParse(rest) with
                | true, amount when amount > 0 ->
                    match adjustCurrency conn state.CharacterId state.RealmSlug (-amount) with
                    | Error _ ->
                        { Success = false
                          Command = "give"
                          Message = $"You do not have {amount} {state.CurrencyNamePlural}."
                          State = Some state }
                    | Ok _ ->
                        adjustCurrency conn targetId state.RealmSlug amount |> ignore
                        let message = $"You give {amount} {state.CurrencyNamePlural} to {targetDisplayName}."
                        logEvent conn userId state.CharacterId state.RoomId "give-currency" (Some rest) message (payloadOf [ "to", targetDisplayName; "amount", string amount ])
                        let nextState = getState userId |> Option.defaultValue state
                        { Success = true
                          Command = "give"
                          Message = message
                          State = Some nextState }
                | _ ->
                    match tryFindItem state.InventoryItems rest with
                    | None ->
                        { Success = false
                          Command = "give"
                          Message = $"You are not carrying '{rest}'."
                          State = Some state }
                    | Some item ->
                        use updateCmd = new NpgsqlCommand(
                            """UPDATE mud_items SET owner_character_id = @target_id
                               WHERE owner_character_id = @character_id AND slug = @slug
                               AND id = (SELECT id FROM mud_items WHERE owner_character_id = @character_id AND slug = @slug ORDER BY position LIMIT 1)""", conn)
                        updateCmd.Parameters.AddWithValue("target_id", targetId) |> ignore
                        updateCmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                        updateCmd.Parameters.AddWithValue("slug", item.Slug) |> ignore
                        updateCmd.ExecuteNonQuery() |> ignore
                        let message = $"You give {item.Name} to {targetDisplayName}."
                        logEvent conn userId state.CharacterId state.RoomId "give-item" (Some rest) message (payloadOf [ "to", targetDisplayName; "item", item.Slug ])
                        let nextState = getState userId |> Option.defaultValue state
                        { Success = true
                          Command = "give"
                          Message = message
                          State = Some nextState })

let getItem (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "get"
              Message = "Get what?"
              State = Some state }
        else
            match tryFindItem state.VisibleItems trimmed with
            | None ->
                { Success = false
                  Command = $"get {trimmed}"
                  Message = $"You do not see '{trimmed}' here."
                  State = Some state }
            | Some item when not item.Portable ->
                { Success = false
                  Command = $"get {trimmed}"
                  Message = $"{item.Name} cannot be carried."
                  State = Some state }
            | Some item ->
                use conn = openConnection ()
                use cmd = new NpgsqlCommand(
                    """UPDATE mud_items
                       SET owner_character_id = @character_id,
                           room_id = NULL
                       WHERE owner_character_id IS NULL
                         AND room_id = @room_id
                         AND lower(slug) = lower(@query)""", conn)
                cmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                cmd.Parameters.AddWithValue("room_id", state.RoomId) |> ignore
                cmd.Parameters.AddWithValue("query", item.Slug) |> ignore
                let changed = cmd.ExecuteNonQuery()
                let nextState = getState userId |> Option.defaultValue state
                let message =
                    if changed > 0 then $"You pick up {item.Name}."
                    else $"You cannot pick up {item.Name} right now."
                if changed > 0 && isResourceSlug conn item.Slug then
                    awardAchievementBySlug userId "mud-scrounger"
                    let resourceCount =
                        nextState.InventoryItems
                        |> List.filter (fun inventoryItem -> isResourceSlug conn inventoryItem.Slug)
                        |> List.length
                    if resourceCount >= 5 then
                        awardAchievementBySlug userId "mud-quartermaster"
                logEvent conn userId state.CharacterId state.RoomId "get" (Some trimmed) message (payloadOf [ "target", trimmed ])
                { Success = changed > 0
                  Command = $"get {trimmed}"
                  Message = message
                  State = Some nextState })

let dropItem (userId: Guid) (query: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "drop"
              Message = "Drop what?"
              State = Some state }
        else
            match tryFindItem state.InventoryItems trimmed with
            | None ->
                { Success = false
                  Command = $"drop {trimmed}"
                  Message = $"You are not carrying '{trimmed}'."
                  State = Some state }
            | Some item ->
                use conn = openConnection ()
                use cmd = new NpgsqlCommand(
                    """UPDATE mud_items
                       SET owner_character_id = NULL,
                           room_id = @room_id
                       WHERE owner_character_id = @character_id
                         AND lower(slug) = lower(@query)""", conn)
                cmd.Parameters.AddWithValue("room_id", state.RoomId) |> ignore
                cmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
                cmd.Parameters.AddWithValue("query", item.Slug) |> ignore
                let changed = cmd.ExecuteNonQuery()
                let nextState = getState userId |> Option.defaultValue state
                let message =
                    if changed > 0 then $"You drop {item.Name}."
                    else $"You cannot drop {item.Name} right now."
                logEvent conn userId state.CharacterId state.RoomId "drop" (Some trimmed) message (payloadOf [ "target", trimmed ])
                { Success = changed > 0
                  Command = $"drop {trimmed}"
                  Message = message
                  State = Some nextState })

let say (userId: Guid) (text: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = text.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "say"
              Message = "Say what?"
              State = Some state }
        else
            use conn = openConnection ()
            let result = MudChatRepository.postRoom userId trimmed
            let message =
                match result with
                | Ok text -> text
                | Error text -> text
            logEvent conn userId state.CharacterId state.RoomId "say" (Some trimmed) message (payloadOf [ "text", trimmed ])
            { Success = result.IsOk
              Command = "say"
              Message = message
              State = Some state })

let emote (userId: Guid) (text: string) : MudCommandResult =
    withState userId (fun state ->
        let trimmed = text.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "emote"
              Message = "Emote what?"
              State = Some state }
        else
            let message = $"{state.CharacterName} {trimmed}"
            use conn = openConnection ()
            logEvent conn userId state.CharacterId state.RoomId "emote" (Some trimmed) message (payloadOf [ "text", trimmed ])
            { Success = true
              Command = "emote"
              Message = message
              State = Some state })

let handleCommand (userId: Guid) (commandText: string) : MudCommandResult =
    let trimmed = if isNull commandText then "" else commandText.Trim()
    if String.IsNullOrWhiteSpace(trimmed) then
        { Success = false
          Command = ""
          Message = "Type look, search, recipes, craft <thing>, examine <thing>, read <thing>, talk <thing>, get <thing>, drop <thing>, inventory, move <direction>, say <message>, shout <message>, whisper <character> <message>, party create/invite/who/leave, gsay <message>, emote <action>, shop, buy <thing>, sell <thing>, balance, give <character> <amount|item>, or direct the builders with @headmaster/@hm or @firstspeaker/@fs."
          State = getState userId }
    else
        let parts = trimmed.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
        let verb = parts.[0].ToLowerInvariant()
        match verb with
        | "look" | "l" -> look userId
        | "search" | "scan" -> search userId
        | "recipes" | "recipe" -> recipes userId
        | "shop" | "list" -> shop userId
        | "buy" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            buyItem userId query
        | "sell" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            sellItem userId query
        | "balance" | "coins" | "wallet" -> checkBalance userId
        | "give" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            give userId query
        | "craft" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            craft userId query
        | "examine" | "exam" | "x" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            examine userId query
        | "read" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            read userId query
        | "talk" | "ask" | "speak" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            talk userId query
        | "inventory" | "inv" | "i" -> inventory userId
        | "get" | "take" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            getItem userId query
        | "drop" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            dropItem userId query
        | "say" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            say userId message
        | "emote" | "me" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            emote userId message
        | "shout" | "yell" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            let result = MudChatRepository.postShout userId message
            match result with
            | Ok text -> { Success = true; Command = "shout"; Message = text; State = getState userId }
            | Error text -> { Success = false; Command = "shout"; Message = text; State = getState userId }
        | "whisper" | "w" | "tell" ->
            let target = if parts.Length > 1 then parts.[1] else ""
            let message = if parts.Length > 2 then String.Join(" ", parts.[2..]) else ""
            let result = MudChatRepository.postWhisper userId target message
            match result with
            | Ok text -> { Success = true; Command = "whisper"; Message = text; State = getState userId }
            | Error text -> { Success = false; Command = "whisper"; Message = text; State = getState userId }
        | "gsay" | "g" | "psay" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            let result = MudChatRepository.postGroup userId message
            match result with
            | Ok text -> { Success = true; Command = "gsay"; Message = text; State = getState userId }
            | Error text -> { Success = false; Command = "gsay"; Message = text; State = getState userId }
        | "party" ->
            let subcommand = if parts.Length > 1 then parts.[1].ToLowerInvariant() else ""
            let rest = if parts.Length > 2 then String.Join(" ", parts.[2..]) else ""
            let result =
                match subcommand with
                | "create" | "form" -> MudChatRepository.partyCreate userId rest
                | "invite" | "add" -> MudChatRepository.partyInvite userId rest
                | "leave" | "quit" -> MudChatRepository.partyLeave userId
                | "who" | "" -> MudChatRepository.partyWho userId
                | other -> Error $"Unknown party command '{other}'. Try: party create <name>, party invite <character>, party who, party leave."
            match result with
            | Ok text -> { Success = true; Command = trimmed; Message = text; State = getState userId }
            | Error text -> { Success = false; Command = trimmed; Message = text; State = getState userId }
        | "move" | "go" ->
            let direction = if parts.Length > 1 then parts.[1] else ""
            if String.IsNullOrWhiteSpace(direction) then
                { Success = false
                  Command = trimmed
                  Message = "Move where?"
                  State = getState userId }
            else
                move userId direction
        | "enter" ->
            let direction = if parts.Length > 1 then parts.[1] else ""
            if String.IsNullOrWhiteSpace(direction) then
                { Success = false
                  Command = trimmed
                  Message = "Enter where?"
                  State = getState userId }
            else
                move userId direction
        | "north" | "south" | "east" | "west" | "up" | "down" | "in" | "out" | "portal"
        | "medieval" | "sci-fi" | "the-veil" | "the-wild-march" | "the-drowned-reach" ->
            move userId verb
        | _ ->
            { Success = false
              Command = trimmed
              Message = "Unknown command. Try look, search, recipes, craft <thing>, examine <thing>, read <thing>, talk <thing>, get <thing>, drop <thing>, inventory, move <direction>, say <message>, shout <message>, whisper <character> <message>, party create/invite/who/leave, gsay <message>, emote <action>, or direct the builders with @headmaster/@hm or @firstspeaker/@fs."
              State = getState userId }
