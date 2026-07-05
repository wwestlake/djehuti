module Djehuti.Api.MudAdminRepository

open System
open System.Data.Common
open System.Text.RegularExpressions
open Npgsql
open Database

type MudZone =
    { Id: Guid
      Name: string
      Slug: string
      Description: string option
      Position: int
      CreatedAt: DateTime }

type MudRoom =
    { Id: Guid
      ZoneId: Guid
      ZoneName: string
      ZoneSlug: string
      Name: string
      Slug: string
      Description: string option
      Position: int
      CreatedAt: DateTime }

type MudExit =
    { Id: Guid
      FromRoomId: Guid
      FromRoomName: string
      FromRoomSlug: string
      ToRoomId: Guid
      ToRoomName: string
      ToRoomSlug: string
      Direction: string
      ExitType: string
      Label: string option
      CreatedAt: DateTime }

type MudWorld =
    { Zones: MudZone list
      Rooms: MudRoom list
      Exits: MudExit list }

type MudRecipeIngredient =
    { Slug: string
      Quantity: int
      Position: int }

type MudRecipe =
    { Id: Guid
      Slug: string
      Name: string
      OutputName: string
      OutputSlug: string
      OutputDescription: string
      OutputReadableText: string option
      Position: int
      Active: bool
      CreatedAt: DateTime
      Ingredients: MudRecipeIngredient list }

type MudItem =
    { Id: Guid
      RoomId: Guid option
      RoomName: string option
      RoomSlug: string option
      Name: string
      Slug: string
      Description: string option
      ReadableText: string option
      Portable: bool
      Position: int
      CreatedAt: DateTime }

type MudBuilderAgent =
    { Id: Guid
      Slug: string
      RealmSlug: string
      DirectorSlug: string
      DisplayName: string
      Specialty: string
      Model: string
      BuildHourUtc: int
      Active: bool
      CreatedAt: DateTime }

type MudRealmMetric =
    { RealmSlug: string
      CharacterCount: int }

type MudExitTypeMetric =
    { ExitType: string
      Count: int }

type MudAdminMetrics =
    { ZoneCount: int
      RoomCount: int
      ExitCount: int
      RecipeCount: int
      ItemCount: int
      PortableItemCount: int
      ReadableItemCount: int
      ActiveCharacterCount: int
      RetiredCharacterCount: int
      CompanionEnabledCount: int
      ByoKeyCount: int
      EmptyZoneCount: int
      DeadEndRoomCount: int
      AverageExitsPerRoom: float
      RealmCharacterCounts: MudRealmMetric list
      ExitTypeCounts: MudExitTypeMetric list }

let private readZone (r: DbDataReader) =
    { Id = r.GetGuid(0)
      Name = r.GetString(1)
      Slug = r.GetString(2)
      Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
      Position = r.GetInt32(4)
      CreatedAt = r.GetFieldValue<DateTime>(5) }

let private readItem (r: DbDataReader) =
    { Id = r.GetGuid(0)
      RoomId = if r.IsDBNull(1) then None else Some (r.GetGuid(1))
      RoomName = if r.IsDBNull(2) then None else Some (r.GetString(2))
      RoomSlug = if r.IsDBNull(3) then None else Some (r.GetString(3))
      Name = r.GetString(4)
      Slug = r.GetString(5)
      Description = if r.IsDBNull(6) then None else Some (r.GetString(6))
      ReadableText = if r.IsDBNull(7) then None else Some (r.GetString(7))
      Portable = r.GetBoolean(8)
      Position = r.GetInt32(9)
      CreatedAt = r.GetFieldValue<DateTime>(10) }

let private readBuilderAgent (r: DbDataReader) =
    { Id = r.GetGuid(0)
      Slug = r.GetString(1)
      RealmSlug = r.GetString(2)
      DirectorSlug = r.GetString(3)
      DisplayName = r.GetString(4)
      Specialty = r.GetString(5)
      Model = r.GetString(6)
      BuildHourUtc = r.GetInt32(7)
      Active = r.GetBoolean(8)
      CreatedAt = r.GetFieldValue<DateTime>(9) }

let private readRoom (r: DbDataReader) =
    { Id = r.GetGuid(0)
      ZoneId = r.GetGuid(1)
      ZoneName = r.GetString(2)
      ZoneSlug = r.GetString(3)
      Name = r.GetString(4)
      Slug = r.GetString(5)
      Description = if r.IsDBNull(6) then None else Some (r.GetString(6))
      Position = r.GetInt32(7)
      CreatedAt = r.GetFieldValue<DateTime>(8) }

let private readExit (r: DbDataReader) =
    { Id = r.GetGuid(0)
      FromRoomId = r.GetGuid(1)
      FromRoomName = r.GetString(2)
      FromRoomSlug = r.GetString(3)
      ToRoomId = r.GetGuid(4)
      ToRoomName = r.GetString(5)
      ToRoomSlug = r.GetString(6)
      Direction = r.GetString(7)
      ExitType = r.GetString(8)
      Label = if r.IsDBNull(9) then None else Some (r.GetString(9))
      CreatedAt = r.GetFieldValue<DateTime>(10) }

let private readRecipeBase (r: DbDataReader) =
    { Id = r.GetGuid(0)
      Slug = r.GetString(1)
      Name = r.GetString(2)
      OutputName = r.GetString(3)
      OutputSlug = r.GetString(4)
      OutputDescription = r.GetString(5)
      OutputReadableText = if r.IsDBNull(6) then None else Some (r.GetString(6))
      Position = r.GetInt32(7)
      Active = r.GetBoolean(8)
      CreatedAt = r.GetFieldValue<DateTime>(9)
      Ingredients = [] }

let private slugify (value: string) =
    let normalized =
        if isNull value then ""
        else value.Trim().ToLowerInvariant()
    let replaced =
        Regex.Replace(normalized, "[^a-z0-9]+", "-")
    replaced.Trim('-')

let private cleanSlug (fallback: string) (value: string) =
    let slug = slugify value
    if String.IsNullOrWhiteSpace slug then slugify fallback else slug

let private nonBlank (value: string) =
    if String.IsNullOrWhiteSpace value then None else Some (value.Trim())

let getWorld () =
    use conn = openConnection ()
    use zonesCmd = new NpgsqlCommand("SELECT id, name, slug, description, position, created_at FROM mud_zones ORDER BY position, name", conn)
    use zonesReader = zonesCmd.ExecuteReader()
    let zones =
        [ while zonesReader.Read() do
            yield readZone zonesReader ]
    zonesReader.Close()

    use roomsCmd = new NpgsqlCommand(
        """SELECT r.id, r.zone_id, z.name, z.slug, r.name, r.slug, r.description, r.position, r.created_at
           FROM mud_rooms r
           JOIN mud_zones z ON z.id = r.zone_id
           ORDER BY z.position, r.position, r.name""", conn)
    use roomsReader = roomsCmd.ExecuteReader()
    let rooms =
        [ while roomsReader.Read() do
            yield readRoom roomsReader ]
    roomsReader.Close()

    use exitsCmd = new NpgsqlCommand(
        """SELECT e.id,
                  e.from_room_id,
                  rf.name,
                  rf.slug,
                  e.to_room_id,
                  rt.name,
                  rt.slug,
                  e.direction,
                  e.exit_type,
                  e.label,
                  e.created_at
           FROM mud_exits e
           JOIN mud_rooms rf ON rf.id = e.from_room_id
           JOIN mud_rooms rt ON rt.id = e.to_room_id
           ORDER BY rf.name, e.direction""", conn)
    use exitsReader = exitsCmd.ExecuteReader()
    let exits =
        [ while exitsReader.Read() do
            yield readExit exitsReader ]

    { Zones = zones; Rooms = rooms; Exits = exits }

let getRecipes () =
    use conn = openConnection ()
    MudRepository.ensureCraftRecipeCatalogSeeded conn |> ignore
    use cmd = new NpgsqlCommand(
        """SELECT r.id,
                  r.slug,
                  r.name,
                  r.output_name,
                  r.output_slug,
                  r.output_description,
                  r.output_readable_text,
                  r.sort_order,
                  r.active,
                  r.created_at,
                  i.ingredient_slug,
                  i.quantity,
                  i.position
           FROM mud_craft_recipes r
           LEFT JOIN mud_craft_recipe_ingredients i ON i.recipe_id = r.id
           ORDER BY r.sort_order, r.name, i.position, i.ingredient_slug""", conn)
    use reader = cmd.ExecuteReader()
    let recipes = ResizeArray<MudRecipe>()
    let byId = Collections.Generic.Dictionary<Guid, int>()

    while reader.Read() do
        let recipeId = reader.GetGuid(0)
        let recipeIndex =
            match byId.TryGetValue(recipeId) with
            | true, index -> index
            | false, _ ->
                let index = recipes.Count
                recipes.Add(readRecipeBase reader)
                byId.Add(recipeId, index)
                index

        if not (reader.IsDBNull(10)) then
            let ingredient =
                { Slug = reader.GetString(10)
                  Quantity = reader.GetInt32(11)
                  Position = reader.GetInt32(12) }
            let recipe = recipes.[recipeIndex]
            recipes.[recipeIndex] <- { recipe with Ingredients = recipe.Ingredients @ [ ingredient ] }

    recipes |> Seq.toList

let private upsertRecipeIngredients (conn: NpgsqlConnection) (recipeId: Guid) (ingredients: MudRecipeIngredient list) =
    use deleteCmd = new NpgsqlCommand("DELETE FROM mud_craft_recipe_ingredients WHERE recipe_id = @recipe_id", conn)
    deleteCmd.Parameters.AddWithValue("recipe_id", recipeId) |> ignore
    deleteCmd.ExecuteNonQuery() |> ignore

    for ingredient in ingredients do
        use insertCmd = new NpgsqlCommand(
            """INSERT INTO mud_craft_recipe_ingredients (recipe_id, ingredient_slug, quantity, position)
               VALUES (@recipe_id, @ingredient_slug, @quantity, @position)""", conn)
        insertCmd.Parameters.AddWithValue("recipe_id", recipeId) |> ignore
        insertCmd.Parameters.AddWithValue("ingredient_slug", ingredient.Slug.Trim().ToLowerInvariant()) |> ignore
        insertCmd.Parameters.AddWithValue("quantity", max 1 ingredient.Quantity) |> ignore
        insertCmd.Parameters.AddWithValue("position", ingredient.Position) |> ignore
        insertCmd.ExecuteNonQuery() |> ignore

let createRecipe (recipe: MudRecipe) =
    use conn = openConnection ()
    MudRepository.ensureCraftRecipeCatalogSeeded conn |> ignore
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_craft_recipes (slug, name, output_name, output_slug, output_description, output_readable_text, sort_order, active)
           VALUES (@slug, @name, @output_name, @output_slug, @output_description, @output_readable_text, @sort_order, @active)
           RETURNING id, slug, name, output_name, output_slug, output_description, output_readable_text, sort_order, active, created_at""", conn)
    cmd.Parameters.AddWithValue("slug", cleanSlug recipe.Name recipe.Slug) |> ignore
    cmd.Parameters.AddWithValue("name", recipe.Name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_name", recipe.OutputName.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_slug", cleanSlug recipe.OutputName recipe.OutputSlug) |> ignore
    cmd.Parameters.AddWithValue("output_description", recipe.OutputDescription.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_readable_text", recipe.OutputReadableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("sort_order", recipe.Position) |> ignore
    cmd.Parameters.AddWithValue("active", recipe.Active) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let created = readRecipeBase reader
            reader.Close()
            upsertRecipeIngredients conn created.Id recipe.Ingredients
            Some { created with Ingredients = recipe.Ingredients }
        else None
    with _ ->
        None

let updateRecipe (recipeId: Guid) (recipe: MudRecipe) =
    use conn = openConnection ()
    MudRepository.ensureCraftRecipeCatalogSeeded conn |> ignore
    use cmd = new NpgsqlCommand(
        """UPDATE mud_craft_recipes
           SET slug = @slug,
               name = @name,
               output_name = @output_name,
               output_slug = @output_slug,
               output_description = @output_description,
               output_readable_text = @output_readable_text,
               sort_order = @sort_order,
               active = @active
           WHERE id = @id
           RETURNING id, slug, name, output_name, output_slug, output_description, output_readable_text, sort_order, active, created_at""", conn)
    cmd.Parameters.AddWithValue("id", recipeId) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug recipe.Name recipe.Slug) |> ignore
    cmd.Parameters.AddWithValue("name", recipe.Name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_name", recipe.OutputName.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_slug", cleanSlug recipe.OutputName recipe.OutputSlug) |> ignore
    cmd.Parameters.AddWithValue("output_description", recipe.OutputDescription.Trim()) |> ignore
    cmd.Parameters.AddWithValue("output_readable_text", recipe.OutputReadableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("sort_order", recipe.Position) |> ignore
    cmd.Parameters.AddWithValue("active", recipe.Active) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let updated = readRecipeBase reader
            reader.Close()
            upsertRecipeIngredients conn recipeId recipe.Ingredients
            Some { updated with Ingredients = recipe.Ingredients }
        else None
    with _ ->
        None

let deleteRecipe (recipeId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM mud_craft_recipes WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", recipeId) |> ignore
    cmd.ExecuteNonQuery() > 0

let createZone (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_zones (name, slug, description, position)
           VALUES (@name, @slug, @description, @position)
           RETURNING id, name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readZone reader) else None
    with _ ->
        None

let updateZone (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_zones
           SET name = @name,
               slug = @slug,
               description = @description,
               position = @position
           WHERE id = @id
           RETURNING id, name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readZone reader) else None
    with _ ->
        None

let createRoom (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_rooms (zone_id, name, slug, description, position)
           VALUES (@zone_id, @name, @slug, @description, @position)
           RETURNING id, zone_id, (SELECT name FROM mud_zones WHERE id = @zone_id), (SELECT slug FROM mud_zones WHERE id = @zone_id), name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("zone_id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readRoom reader) else None
    with _ ->
        None

let updateRoom (roomId: Guid) (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_rooms
           SET zone_id = @zone_id,
               name = @name,
               slug = @slug,
               description = @description,
               position = @position
           WHERE id = @id
           RETURNING id, zone_id,
                     (SELECT name FROM mud_zones WHERE id = @zone_id),
                     (SELECT slug FROM mud_zones WHERE id = @zone_id),
                     name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("id", roomId) |> ignore
    cmd.Parameters.AddWithValue("zone_id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readRoom reader) else None
    with _ ->
        None

let createExit (fromRoomId: Guid) (toRoomId: Guid) (direction: string) (exitType: string) (label: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_exits (from_room_id, to_room_id, direction, exit_type, label)
           VALUES (@from_room_id, @to_room_id, @direction, @exit_type, @label)
           RETURNING id, from_room_id,
                     (SELECT name FROM mud_rooms WHERE id = @from_room_id),
                     (SELECT slug FROM mud_rooms WHERE id = @from_room_id),
                     to_room_id,
                     (SELECT name FROM mud_rooms WHERE id = @to_room_id),
                     (SELECT slug FROM mud_rooms WHERE id = @to_room_id),
                     direction, exit_type, label, created_at""", conn)
    cmd.Parameters.AddWithValue("from_room_id", fromRoomId) |> ignore
    cmd.Parameters.AddWithValue("to_room_id", toRoomId) |> ignore
    cmd.Parameters.AddWithValue("direction", direction.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("exit_type", exitType.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("label", label |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readExit reader) else None
    with _ ->
        None

let deleteExit (exitId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM mud_exits WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", exitId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Items

let private itemSelectSql =
    """SELECT i.id, i.room_id, r.name, r.slug, i.name, i.slug, i.description, i.readable_text, i.portable, i.position, i.created_at
       FROM mud_items i
       LEFT JOIN mud_rooms r ON r.id = i.room_id"""

let getItems () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"{itemSelectSql} WHERE i.owner_character_id IS NULL ORDER BY r.name, i.position, i.name", conn)
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readItem reader ]

let createItem
    (roomId: Guid option)
    (name: string)
    (slug: string)
    (description: string option)
    (readableText: string option)
    (portable: bool)
    (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
           VALUES (@room_id, @name, @slug, @description, @readable_text, @portable, @position)
           RETURNING id""", conn)
    cmd.Parameters.AddWithValue("room_id", roomId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("readable_text", readableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("portable", portable) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        let itemId = cmd.ExecuteScalar() :?> Guid
        use selectCmd = new NpgsqlCommand($"{itemSelectSql} WHERE i.id = @id", conn)
        selectCmd.Parameters.AddWithValue("id", itemId) |> ignore
        use reader = selectCmd.ExecuteReader()
        if reader.Read() then Some (readItem reader) else None
    with _ ->
        None

let updateItem
    (itemId: Guid)
    (roomId: Guid option)
    (name: string)
    (slug: string)
    (description: string option)
    (readableText: string option)
    (portable: bool)
    (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_items
           SET room_id = @room_id,
               name = @name,
               slug = @slug,
               description = @description,
               readable_text = @readable_text,
               portable = @portable,
               position = @position
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", itemId) |> ignore
    cmd.Parameters.AddWithValue("room_id", roomId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("readable_text", readableText |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("portable", portable) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        if cmd.ExecuteNonQuery() = 0 then
            None
        else
            use selectCmd = new NpgsqlCommand($"{itemSelectSql} WHERE i.id = @id", conn)
            selectCmd.Parameters.AddWithValue("id", itemId) |> ignore
            use reader = selectCmd.ExecuteReader()
            if reader.Read() then Some (readItem reader) else None
    with _ ->
        None

let deleteItem (itemId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM mud_items WHERE id = @id AND owner_character_id IS NULL", conn)
    cmd.Parameters.AddWithValue("id", itemId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Builder roster (AI construction crew)

let private builderAgentSelectSql =
    """SELECT id, slug, realm_slug, director_slug, display_name, specialty, model, build_hour_utc, active, created_at
       FROM mud_builder_agents"""

let getBuilderAgents () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand($"{builderAgentSelectSql} ORDER BY realm_slug, build_hour_utc, display_name", conn)
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readBuilderAgent reader ]

let createBuilderAgent
    (slug: string)
    (realmSlug: string)
    (directorSlug: string)
    (displayName: string)
    (specialty: string)
    (model: string)
    (buildHourUtc: int)
    (active: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_builder_agents (slug, realm_slug, director_slug, display_name, specialty, model, build_hour_utc, active)
           VALUES (@slug, @realm_slug, @director_slug, @display_name, @specialty, @model, @build_hour_utc, @active)
           RETURNING id""", conn)
    cmd.Parameters.AddWithValue("slug", cleanSlug displayName slug) |> ignore
    cmd.Parameters.AddWithValue("realm_slug", realmSlug.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("director_slug", directorSlug.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("display_name", displayName.Trim()) |> ignore
    cmd.Parameters.AddWithValue("specialty", specialty.Trim()) |> ignore
    cmd.Parameters.AddWithValue("model", if String.IsNullOrWhiteSpace model then "gpt-4o-mini" else model.Trim()) |> ignore
    cmd.Parameters.AddWithValue("build_hour_utc", ((buildHourUtc % 24) + 24) % 24) |> ignore
    cmd.Parameters.AddWithValue("active", active) |> ignore
    try
        let agentId = cmd.ExecuteScalar() :?> Guid
        use selectCmd = new NpgsqlCommand($"{builderAgentSelectSql} WHERE id = @id", conn)
        selectCmd.Parameters.AddWithValue("id", agentId) |> ignore
        use reader = selectCmd.ExecuteReader()
        if reader.Read() then Some (readBuilderAgent reader) else None
    with _ ->
        None

let updateBuilderAgent
    (agentId: Guid)
    (realmSlug: string)
    (directorSlug: string)
    (displayName: string)
    (specialty: string)
    (model: string)
    (buildHourUtc: int)
    (active: bool) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_builder_agents
           SET realm_slug = @realm_slug,
               director_slug = @director_slug,
               display_name = @display_name,
               specialty = @specialty,
               model = @model,
               build_hour_utc = @build_hour_utc,
               active = @active
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", agentId) |> ignore
    cmd.Parameters.AddWithValue("realm_slug", realmSlug.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("director_slug", directorSlug.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("display_name", displayName.Trim()) |> ignore
    cmd.Parameters.AddWithValue("specialty", specialty.Trim()) |> ignore
    cmd.Parameters.AddWithValue("model", if String.IsNullOrWhiteSpace model then "gpt-4o-mini" else model.Trim()) |> ignore
    cmd.Parameters.AddWithValue("build_hour_utc", ((buildHourUtc % 24) + 24) % 24) |> ignore
    cmd.Parameters.AddWithValue("active", active) |> ignore
    try
        if cmd.ExecuteNonQuery() = 0 then
            None
        else
            use selectCmd = new NpgsqlCommand($"{builderAgentSelectSql} WHERE id = @id", conn)
            selectCmd.Parameters.AddWithValue("id", agentId) |> ignore
            use reader = selectCmd.ExecuteReader()
            if reader.Read() then Some (readBuilderAgent reader) else None
    with _ ->
        None

let deleteBuilderAgent (agentId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM mud_builder_agents WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", agentId) |> ignore
    cmd.ExecuteNonQuery() > 0

let getMetrics () =
    use conn = openConnection ()

    use summaryCmd = new NpgsqlCommand(
        """SELECT
               (SELECT COUNT(*) FROM mud_zones),
               (SELECT COUNT(*) FROM mud_rooms),
               (SELECT COUNT(*) FROM mud_exits),
               (SELECT COUNT(*) FROM mud_craft_recipes WHERE active = TRUE),
               (SELECT COUNT(*) FROM mud_items),
               (SELECT COUNT(*) FROM mud_items WHERE portable = TRUE),
               (SELECT COUNT(*) FROM mud_items WHERE readable_text IS NOT NULL AND btrim(readable_text) <> ''),
               (SELECT COUNT(*) FROM mud_characters WHERE deleted_at IS NULL),
               (SELECT COUNT(*) FROM mud_characters WHERE deleted_at IS NOT NULL),
               (SELECT COUNT(*) FROM mud_companion_profiles WHERE enabled = TRUE),
               (SELECT COUNT(*) FROM mud_companion_profiles WHERE byo_openai_key_protected IS NOT NULL),
               (SELECT COUNT(*) FROM mud_zones z WHERE NOT EXISTS (SELECT 1 FROM mud_rooms r WHERE r.zone_id = z.id)),
               (SELECT COUNT(*) FROM mud_rooms r WHERE NOT EXISTS (SELECT 1 FROM mud_exits e WHERE e.from_room_id = r.id)),
               COALESCE((
                   SELECT ROUND(AVG(exit_count)::numeric, 2)::float8
                   FROM (
                       SELECT COUNT(e.id) AS exit_count
                       FROM mud_rooms r
                       LEFT JOIN mud_exits e ON e.from_room_id = r.id
                       GROUP BY r.id
                   ) q
               ), 0.0)""", conn)

    use summaryReader = summaryCmd.ExecuteReader()
    let summary =
        if summaryReader.Read() then
            { ZoneCount = summaryReader.GetInt32(0)
              RoomCount = summaryReader.GetInt32(1)
              ExitCount = summaryReader.GetInt32(2)
              RecipeCount = summaryReader.GetInt32(3)
              ItemCount = summaryReader.GetInt32(4)
              PortableItemCount = summaryReader.GetInt32(5)
              ReadableItemCount = summaryReader.GetInt32(6)
              ActiveCharacterCount = summaryReader.GetInt32(7)
              RetiredCharacterCount = summaryReader.GetInt32(8)
              CompanionEnabledCount = summaryReader.GetInt32(9)
              ByoKeyCount = summaryReader.GetInt32(10)
              EmptyZoneCount = summaryReader.GetInt32(11)
              DeadEndRoomCount = summaryReader.GetInt32(12)
              AverageExitsPerRoom = summaryReader.GetDouble(13)
              RealmCharacterCounts = []
              ExitTypeCounts = [] }
        else
            { ZoneCount = 0
              RoomCount = 0
              ExitCount = 0
              RecipeCount = 0
              ItemCount = 0
              PortableItemCount = 0
              ReadableItemCount = 0
              ActiveCharacterCount = 0
              RetiredCharacterCount = 0
              CompanionEnabledCount = 0
              ByoKeyCount = 0
              EmptyZoneCount = 0
              DeadEndRoomCount = 0
              AverageExitsPerRoom = 0.0
              RealmCharacterCounts = []
              ExitTypeCounts = [] }
    summaryReader.Close()

    use realmCmd = new NpgsqlCommand(
        """SELECT realm_slug, COUNT(*)
           FROM mud_characters
           WHERE deleted_at IS NULL
           GROUP BY realm_slug
           ORDER BY realm_slug""", conn)
    use realmReader = realmCmd.ExecuteReader()
    let realmCounts =
        [ while realmReader.Read() do
            yield
                { RealmSlug = realmReader.GetString(0)
                  CharacterCount = realmReader.GetInt32(1) } ]
    realmReader.Close()

    use exitTypeCmd = new NpgsqlCommand(
        """SELECT exit_type, COUNT(*)
           FROM mud_exits
           GROUP BY exit_type
           ORDER BY COUNT(*) DESC, exit_type""", conn)
    use exitTypeReader = exitTypeCmd.ExecuteReader()
    let exitTypeCounts =
        [ while exitTypeReader.Read() do
            yield
                { ExitType = exitTypeReader.GetString(0)
                  Count = exitTypeReader.GetInt32(1) } ]

    { summary with
        RealmCharacterCounts = realmCounts
        ExitTypeCounts = exitTypeCounts }
