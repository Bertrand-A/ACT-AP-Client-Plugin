using System.Collections.Generic;

namespace ACTAP
{
    public class EnemyData
    {
        // Fixed, ordered pool used as the deterministic target space for the enemy
        // randomizer. Names are NORMALIZED (no "(Clone)", " (12)", "_3" instance
        // suffixes) so they match EnemyRando.NormalizedName(). The deterministic
        // target for an enemy is RandoPool[ stableHash(seed + UUID) % RandoPool.Count ],
        // and a swap only happens once a live template of that target type has been
        // cached this session (so targets that never appear simply don't get used).
        //
        // NOTE: order and contents of this list are part of the seed contract — editing
        // it changes every enemy's mapping for a given seed. Freeze it for a release.
        // These are the types observed in The Shallows (NG+ save). Extend with other
        // regions' enemies as they're catalogued.
        public static readonly List<string> RandoPool = new List<string>()
        {
            "Enemy_Sardine",
            "Enemy_Anchovy",
            "Enemy_Rangoon_Normie",
            "Enemy_Rangoon_Normie_Stunnable Variant",
            "Enemy_Rangoon_Broadsword",
            "Enemy_RangoonSoldier Variant",
            "Enemy_Lobster_Knight",
            "Enemy_Pufferfish",
            "Enemy_Sardine_NG+ Variant",
            "Enemy_Sardine_2_NG+ Variant",
            "Enemy_Sardine_NG+ Variant UmamiGhost",
            "Enemy_Anchovy_NG+ Variant",
            "Enemy_Rangoon_Normie NG+ Variant",
            "Enemy_Rangoon_Normie_Stunnable_NG+ Variant",
            "Enemy_Rangoon_Broadsword_NG+ Variant",
            "Enemy_Rangoon_Thimble_NG+ Variant",
            "Enemy_Rangoon_Parasol_NG+ Variant",
            "Enemy_Rangoon_Martini_NG+ Variant",
            "Enemy_RangoonSoldier_NG+ Variant",
            "Enemy_Bowman_NG+ Variant",
            "Enemy_Pufferfish_NG+ Variant",
            "Enemy_Pufferfish_Bleached_NEW_NG+ Variant",
            "Enemy_Lobster_Knight_NG+ Variant",
            "Enemy_Seahorse_NG+ Variant",
            "Enemy_BobbitWorm_NG+ Variant",
            "Enemy_BobbitWorm_EvilVariant_NG+ Variant",
        };
    }
}
