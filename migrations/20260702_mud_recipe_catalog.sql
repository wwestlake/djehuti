CREATE TABLE IF NOT EXISTS mud_craft_recipes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    output_name TEXT NOT NULL,
    output_slug TEXT NOT NULL,
    output_description TEXT NOT NULL,
    output_readable_text TEXT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS mud_craft_recipe_ingredients (
    recipe_id UUID NOT NULL REFERENCES mud_craft_recipes(id) ON DELETE CASCADE,
    ingredient_slug TEXT NOT NULL,
    quantity INT NOT NULL DEFAULT 1,
    position INT NOT NULL DEFAULT 0,
    PRIMARY KEY (recipe_id, position)
);

CREATE INDEX IF NOT EXISTS idx_mud_craft_recipes_sort_order ON mud_craft_recipes(sort_order, name);
CREATE INDEX IF NOT EXISTS idx_mud_craft_recipe_ingredients_slug ON mud_craft_recipe_ingredients(ingredient_slug);

GRANT ALL ON TABLE mud_craft_recipes TO djehuti;
GRANT ALL ON TABLE mud_craft_recipe_ingredients TO djehuti;
