#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Carpets and furniture made only from graybox shapes (no art assets).
    // Everything is decoration: no colliders, so routes, cover and guard patrols are unchanged.
    // Sorting orders 1-9 keep decor above the floor and below actors, and dark (unseen) rooms still hide it.
    public static partial class CreateColdBrewScene
    {
        private static Transform decor;

        private static readonly Color RugEdge = new Color(.36f, .2f, .24f);
        private static readonly Color RugFill = new Color(.27f, .15f, .19f);
        private static readonly Color RugLine = new Color(.48f, .3f, .33f);
        private static readonly Color Wood = new Color(.42f, .3f, .21f);
        private static readonly Color WoodDark = new Color(.3f, .21f, .15f);
        private static readonly Color Fabric = new Color(.25f, .31f, .38f);
        private static readonly Color FabricDark = new Color(.19f, .24f, .3f);
        private static readonly Color Leaf = new Color(.2f, .42f, .28f);
        private static readonly Color LeafLight = new Color(.27f, .53f, .35f);
        private static readonly Color Pot = new Color(.45f, .3f, .24f);

        private static void BeginDecor()
        {
            GameObject existing = GameObject.Find("Decor");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            decor = Node("Decor", null, Vector2.zero);
        }

        private static Transform Piece(string name, Vector2 position)
        {
            return Node(name, decor, position);
        }

        
        private static void AddFurnitureCollider(Transform furniture, Vector2 size)
        {
            BoxCollider2D collider = furniture.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
        }


        private static void Rug(Vector2 at, Vector2 size)
        {
            Transform rug = Piece("Rug", at);
            Shape("Edge", rug, Vector2.zero, size, PrimitiveKind.Box, RugEdge, 1);
            Shape("Fill", rug, Vector2.zero, size - new Vector2(.3f, .3f), PrimitiveKind.Box, RugFill, 2);
            Shape("Line", rug, Vector2.zero, size - new Vector2(.55f, .55f), PrimitiveKind.Box, RugLine, 3);
            Shape("Center", rug, Vector2.zero, size - new Vector2(.7f, .7f), PrimitiveKind.Box, RugFill, 4);
        }

        // A sofa against a wall. facingDown = backrest on top (for the north wall).
        private static void Sofa(Vector2 at, bool facingDown = true)
        {
            Transform sofa = Piece("Sofa", at);
            AddFurnitureCollider(sofa, new Vector2(2.4f, 0.9f));
            float back = facingDown ? .38f : -.38f;
            Shape("Seat", sofa, Vector2.zero, new Vector2(2.4f, .9f), PrimitiveKind.Box, Fabric, 6);
            Shape("Back", sofa, new Vector2(0, back), new Vector2(2.4f, .3f), PrimitiveKind.Box, FabricDark, 7);
            Shape("Arm L", sofa, new Vector2(-1.1f, 0), new Vector2(.25f, .9f), PrimitiveKind.Box, FabricDark, 7);
            Shape("Arm R", sofa, new Vector2(1.1f, 0), new Vector2(.25f, .9f), PrimitiveKind.Box, FabricDark, 7);
            Shape("Cushion L", sofa, new Vector2(-.5f, -back * .3f), new Vector2(.85f, .45f), PrimitiveKind.Box, Fabric * 1.15f, 8);
            Shape("Cushion R", sofa, new Vector2(.5f, -back * .3f), new Vector2(.85f, .45f), PrimitiveKind.Box, Fabric * 1.15f, 8);
        }

        private static void CafeTable(Vector2 at)
        {
            Transform table = Piece("Cafe Table", at);
            AddFurnitureCollider(table, new Vector2(1.75f, 1.0f));
            Shape("Chair L", table, new Vector2(-.65f, 0), new Vector2(.45f, .45f), PrimitiveKind.Box, WoodDark, 5);
            Shape("Chair R", table, new Vector2(.65f, 0), new Vector2(.45f, .45f), PrimitiveKind.Box, WoodDark, 5);
            Shape("Top", table, Vector2.zero, new Vector2(.85f, .85f), PrimitiveKind.Circle, Wood, 6);
            Shape("Cup", table, new Vector2(.12f, .1f), new Vector2(.18f, .18f), PrimitiveKind.Circle, new Color(.9f, .87f, .82f), 7);
        }

        private static void Plant(Vector2 at)
        {
            Transform plant = Piece("Plant", at);
            AddFurnitureCollider(plant, new Vector2(0.75f, 0.75f));
            Shape("Pot", plant, Vector2.zero, new Vector2(.5f, .5f), PrimitiveKind.Box, Pot, 6);
            Shape("Leaves", plant, new Vector2(0, .05f), new Vector2(.75f, .75f), PrimitiveKind.Circle, Leaf, 7);
            Shape("Leaf A", plant, new Vector2(-.15f, .15f), new Vector2(.35f, .35f), PrimitiveKind.Circle, LeafLight, 8);
            Shape("Leaf B", plant, new Vector2(.18f, -.05f), new Vector2(.3f, .3f), PrimitiveKind.Circle, LeafLight, 8);
        }

        // A bookshelf against a wall, with a row of muted "books".
        private static void Shelf(Vector2 at, float width = 2.2f)
        {
            Transform shelf = Piece("Shelf", at);
            AddFurnitureCollider(shelf, new Vector2(width, 0.5f));
            Shape("Frame", shelf, Vector2.zero, new Vector2(width, .5f), PrimitiveKind.Box, WoodDark, 5);
            Color[] books = { new Color(.45f, .28f, .28f), new Color(.3f, .38f, .45f), new Color(.5f, .45f, .3f), new Color(.32f, .42f, .33f) };
            int count = Mathf.FloorToInt(width / .3f) - 1;
            for (int i = 0; i < count; i++)
                Shape("Book " + i, shelf, new Vector2(-width / 2f + .3f + i * .3f, 0), new Vector2(.2f, .34f),
                    PrimitiveKind.Box, books[i % books.Length], 6);
        }

        // Level 0: Street (x 0-10) and Hideout (x 10-20).
        private static void AddLevel0Decor()
        {
            BeginDecor();
            Rug(new Vector2(6.5f, 2.2f), new Vector2(3.2f, 2f));
            Sofa(new Vector2(3, 9.2f));
            Plant(new Vector2(.8f, .8f));
            Plant(new Vector2(9.2f, 9.2f));
            Rug(new Vector2(15, 5), new Vector2(3.6f, 2.6f));
            Shelf(new Vector2(12.6f, 9.55f));
            CafeTable(new Vector2(12.2f, 6.8f));
            Plant(new Vector2(19.2f, .8f));
        }

        // Level 1 after the Fork is cut: Courtyard (0-10), Corridor (10-20), Hideout (20-30).
        private static void AddLevel1Decor()
        {
            BeginDecor();
            Plant(new Vector2(.8f, 9.2f));
            Plant(new Vector2(.8f, .8f));
            Plant(new Vector2(9.2f, .8f));
            CafeTable(new Vector2(7, 7.8f));
            CafeTable(new Vector2(7, 2.2f));
            Rug(new Vector2(15, 5), new Vector2(7f, 1.3f));
            Plant(new Vector2(19.2f, .8f));
            Shelf(new Vector2(16.5f, .45f));
            Rug(new Vector2(24, 3), new Vector2(3.4f, 2.2f));
            Sofa(new Vector2(23.5f, 9.2f));
            Shelf(new Vector2(28.4f, .45f), 2.4f);
            Plant(new Vector2(29.2f, 9.2f));
        }

        // Called by the Level 1 update tool, which edits an existing scene.
        public static void AddLevel1DecorToOpenScene()
        {
            flat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Flat.mat");
            if (flat == null) throw new InvalidOperationException("Flat.mat is missing.");
            AddLevel1Decor();
        }
    }
}
#endif
