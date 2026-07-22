using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AggroCrab.Enemies;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ACTAP
{
    // Static, deterministic enemy randomizer.
    //
    // Each enemy has a stable per-scene UUID (SaveStateKillableEntity/SaveStateID.UUID).
    // Its replacement type is a pure function of a persisted seed + that UUID over the
    // effective pool, so the SAME physical enemy always becomes the SAME replacement
    // across reloads/deaths/sessions - no re-shuffling.
    //
    // The game exposes no enemy prefab database, so replacements are spawned by cloning a
    // live in-scene instance of the target type (rebuilt each scene load - templates are
    // NOT persisted, because IdleState.InitPatrolRoute reparents scene waypoints and a
    // template parked across scenes ends up referencing destroyed transforms -> NPE).
    // A swap only happens once a live template of its target type is loaded; otherwise it
    // is deferred and retried on the next sceneLoaded (which re-scans all loaded scenes).
    //
    // The clone INHERITS the source enemy's UUID so kill-tracking (obj_state_<UUID>) stays
    // tied to the original: kill a randomized enemy and, on reload, the game deletes the
    // "already killed" original before we ever process it - it stays dead.
    public static class EnemyRando
    {
        // Debug-mode-only overrides (no live AP connection => no slot_data to read). Ignored
        // once connected; the real setting then comes from the "Enemy Randomizer" YAML option
        // via slot_data -> CrabFile ("enemyRando", see SaveSettingsToFile.cs).
        public static bool debugEnabled = true;
        public static bool debugIncludeNgPlus = false;

        static readonly Regex InstanceSuffix = new Regex(@"(\(Clone\)|\s\(\d+\)|_\d+)$");
        static readonly string[] ExcludedKeywords = { "boss", "executioner", "crystal", "npc" };
        static readonly string[] ExcludedScenes = { "Title", "Pretitle", "Loading", "Player_Main" };

        const string RandoSuffix = "_RANDO";

        static int seed;
        static bool initialized;

        // Target types whose spawn threw (e.g. an enemy type we can't safely clone); skipped
        // from then on so a single bad type can't spam errors or vanish enemies every load.
        static readonly HashSet<string> failedTargetTypes = new HashSet<string>();

        public static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[EnemyRando] Initialize() subscribed to SceneManager.sceneLoaded.");
        }

        // 0 = off, 1 = on (base pool), 2 = on with NG+ pool included.
        static int Mode()
        {
            if (Plugin.debugMode)
            {
                return debugEnabled ? (debugIncludeNgPlus ? 2 : 1) : 0;
            }
            return CrabFile.current != null ? CrabFile.current.GetInt("enemyRando") : 0;
        }

        static bool IsActive()
        {
            return Mode() > 0;
        }

        static void EnsureInit()
        {
            if (initialized || CrabFile.current == null)
            {
                return;
            }

            seed = CrabFile.current.GetInt("enemyRandoSeed");
            if (seed == 0)
            {
                seed = new System.Random().Next(1, int.MaxValue);
                CrabFile.current.SetInt("enemyRandoSeed", seed);
                Debug.Log($"[EnemyRando] Generated new enemy rando seed: {seed}");
            }
            else
            {
                Debug.Log($"[EnemyRando] Loaded enemy rando seed: {seed}");
            }
            initialized = true;
        }

        static List<string> EffectivePool()
        {
            if (Mode() == 2)
            {
                return EnemyData.RandoPool;
            }
            return EnemyData.RandoPool.Where(n => !IsNgPlus(n)).ToList();
        }

        static bool IsNgPlus(string name)
        {
            return name.IndexOf("NG+", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsActive() || ExcludedScenes.Contains(scene.name))
            {
                return;
            }

            EnsureInit();
            if (!initialized)
            {
                Debug.Log("[EnemyRando] Skipping: CrabFile not ready (no seed).");
                return;
            }

            List<string> pool = EffectivePool();
            if (pool.Count == 0)
            {
                return;
            }

            Enemy[] all = UnityEngine.Object.FindObjectsOfType<Enemy>(true);

            // Live in-scene template per pool type (any instance, including already-swapped
            // _RANDO clones since those are valid enemies of their type). Prefer enabled ones.
            Dictionary<string, Enemy> templates = BuildTemplates(all, pool);

            List<Enemy> candidates = all.Where(IsSwapCandidate).ToList();
            int swaps = 0, deferred = 0, rolledSelf = 0, noUuid = 0, failed = 0;
            foreach (Enemy source in candidates)
            {
                if (source == null)
                {
                    continue;
                }

                string uuid = GetUuid(source);
                if (string.IsNullOrEmpty(uuid))
                {
                    noUuid++;
                    continue;
                }

                string target = DeterministicTarget(uuid, pool);
                if (target == null || target == NormalizedName(source) || failedTargetTypes.Contains(target))
                {
                    rolledSelf++;
                    continue;
                }

                if (!templates.TryGetValue(target, out Enemy template) || template == null)
                {
                    deferred++;
                    continue;
                }

                try
                {
                    SwapEnemy(source, template, target, uuid);
                    swaps++;
                }
                catch (Exception ex)
                {
                    failedTargetTypes.Add(target);
                    failed++;
                    Debug.LogError($"[EnemyRando] Swap to '{target}' failed, blacklisting that type: {ex.Message}");
                }
            }

            Debug.Log($"[EnemyRando] {scene.name}: mode={Mode()}, pool={pool.Count}, templates={templates.Count}, candidates={candidates.Count}, swaps={swaps}, deferred={deferred}, rolledSelf={rolledSelf}, noUuid={noUuid}, failed={failed}");
        }

        static Dictionary<string, Enemy> BuildTemplates(Enemy[] all, List<string> pool)
        {
            Dictionary<string, Enemy> templates = new Dictionary<string, Enemy>();
            foreach (Enemy e in all)
            {
                if (e == null || IsExcluded(e))
                {
                    continue;
                }
                string type = NormalizedName(e);
                if (!pool.Contains(type))
                {
                    continue;
                }
                // Keep the first instance found, but upgrade to an enabled one if we had a
                // culled placeholder.
                if (!templates.TryGetValue(type, out Enemy current) || current == null)
                {
                    templates[type] = e;
                }
                else if (!current.enabled && e.enabled)
                {
                    templates[type] = e;
                }
            }
            return templates;
        }

        static void SwapEnemy(Enemy source, Enemy template, string targetType, string uuid)
        {
            Vector3 pos = source.transform.position;
            Quaternion rot = source.transform.rotation;
            Transform parent = source.transform.parent;

            // Clone the live template first (do NOT destroy the source until this succeeds, so
            // a throw can never leave a hole). Cloning from a live in-scene instance keeps its
            // IdleState patrol waypoints valid.
            Enemy clone = UnityEngine.Object.Instantiate(template, pos, rot);
            clone.gameObject.name = targetType + RandoSuffix;
            clone.gameObject.SetActive(true);
            if (parent != null)
            {
                clone.transform.parent = parent;
            }

            // Inherit the original's stable UUID (clone starts with the template's UUID -
            // overwrite it) BEFORE removing the source and BEFORE ReAwaken re-registers the
            // save state, so kill-tracking follows the original and no duplicate UUID lingers.
            SaveStateKillableEntity ss = clone.GetComponent<SaveStateKillableEntity>();
            if (ss != null)
            {
                ss.UUID = uuid;
            }

            UnityEngine.Object.DestroyImmediate(source.gameObject);

            // Do NOT ReAwaken here. Instantiating an active, healthy template already runs the
            // clone's own full Awake/OnEnable/Start once (correct scale, drag, AI, health) -
            // calling ReAwaken would re-run all of that a SECOND time, double-initializing the
            // enemy (broken physics: huge velocity / wrong drag, and scale/AI artifacts).
            // Just guarantee the component is enabled: no-op if the clone already initialized,
            // and if the template happened to be culled (disabled) this fires its single
            // pending OnEnable+Start. Either way the clone initializes exactly once.
            clone.enabled = true;

            // Fix the drag baseline. Enemy.InitDrag does `dragXZ = rb.drag; rb.drag = 0`
            // (drag is applied manually from Character.dragXZ in Enemy.UpdateDrag). The
            // template's rigidbody therefore has drag 0, and dragXZ is NON-serialized, so the
            // clone's own InitDrag captured dragXZ = 0 -> no horizontal braking -> enemies
            // move far too fast. Copy the template's correct captured baseline over.
            float templateDragXZ = Traverse.Create(template).Field("dragXZ").GetValue<float>();
            if (templateDragXZ > 0f)
            {
                Traverse.Create(clone).Field("dragXZ").SetValue(templateDragXZ);
            }
        }

        static string DeterministicTarget(string uuid, List<string> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }
            uint h = StableHash(seed + "|" + uuid);
            return pool[(int)(h % (uint)pool.Count)];
        }

        // FNV-1a: stable across runs/platforms (unlike string.GetHashCode).
        static uint StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 16777619;
                }
                return h;
            }
        }

        static bool IsSwapCandidate(Enemy e)
        {
            if (e == null)
            {
                return false;
            }
            if (e.gameObject.name.Contains(RandoSuffix))
            {
                return false;
            }
            return !IsExcluded(e);
        }

        static string GetUuid(Enemy e)
        {
            SaveStateKillableEntity ss = e.GetComponent<SaveStateKillableEntity>();
            return ss != null ? ss.UUID : null;
        }

        static bool IsExcluded(Enemy enemy)
        {
            if (enemy.isBoss)
            {
                return true;
            }

            string rawName = enemy.gameObject.name;

            // Only real hostile mobs follow the "Enemy_" naming convention. FindObjectsOfType
            // <Enemy> also returns crystal deposits (BigCrystal/HugeCrystal), dialogue NPCs
            // and static decor doubles that derive from Enemy but aren't valid swap targets.
            if (!rawName.StartsWith("Enemy_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string lower = rawName.ToLowerInvariant();
            return ExcludedKeywords.Any(lower.Contains);
        }

        static string NormalizedName(Enemy enemy)
        {
            string raw = enemy.gameObject.name.Replace("(Clone)", "").Trim();
            raw = raw.Replace(RandoSuffix, "").Trim();
            raw = InstanceSuffix.Replace(raw, "").Trim();
            return raw;
        }

        // ---- F7 diagnostic (works while connected; see Plugin PlayerPatch) ----

        public static void DumpNearestEnemyState(Vector3 playerPosition)
        {
            Enemy nearest = UnityEngine.Object.FindObjectsOfType<Enemy>(true)
                .Where(e => e != null)
                .OrderBy(e => Vector3.Distance(e.transform.position, playerPosition))
                .FirstOrDefault();

            if (nearest == null)
            {
                Debug.Log("[EnemyRando] DumpNearestEnemyState: no enemy found.");
                return;
            }

            DumpOne("NEAREST", nearest);

            Enemy nearestRando = UnityEngine.Object.FindObjectsOfType<Enemy>(true)
                .Where(e => e != null && e.gameObject.name.Contains(RandoSuffix))
                .OrderBy(e => Vector3.Distance(e.transform.position, playerPosition))
                .FirstOrDefault();
            if (nearestRando != null && nearestRando != nearest)
            {
                DumpOne("NEAREST_RANDO", nearestRando);
            }
        }

        static void DumpOne(string label, Enemy e)
        {
            Traverse t = Traverse.Create(e);
            Debug.Log($"[EnemyRando] --- {label}: '{e.gameObject.name}' ---");
            Debug.Log($"[EnemyRando]   pos={e.transform.position}, layer={e.gameObject.layer}, activeInHierarchy={e.gameObject.activeInHierarchy}, enabled={e.enabled}");
            Debug.Log($"[EnemyRando]   aggro={e.aggro}, dead={e.dead}, canBeDamaged={e.canBeDamaged}, isInvincible={e.isInvincible}, health={e.health}");
            Debug.Log($"[EnemyRando]   localScale={e.transform.localScale}, lossyScale={e.transform.lossyScale}");
            var scaler = e.GetComponentInChildren<EnemyScaler>(true);
            if (scaler != null)
            {
                Debug.Log($"[EnemyRando]   EnemyScaler.sizeScale={scaler.sizeScale}, randomRange={scaler.randomRange}, scaleMinMax={scaler.scaleMinMax}");
            }
            DumpProp(t, "state");
            DumpProp(t, "newTarget");
            DumpProp(t, "aggroTarget");

            Rigidbody rb = e.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"[EnemyRando]   rb.velocity={rb.velocity}, angularVelocity={rb.angularVelocity}, isKinematic={rb.isKinematic}");
            }

            DumpField(t, "_newTarget");
            DumpField(t, "cachedAggroTargetPosition");
            DumpField(t, "dragXZ");

            try
            {
                var allEnemies = Traverse.Create(typeof(Enemy)).Field("allEnemies").GetValue() as System.Collections.IList;
                var activeEnemies = Traverse.Create(typeof(Enemy)).Field("activeEnemies").GetValue() as System.Collections.IList;
                bool inAll = allEnemies != null && allEnemies.Contains(e);
                bool inActive = activeEnemies != null && activeEnemies.Contains(e);
                Debug.Log($"[EnemyRando]   inAllEnemies={inAll}, inActiveEnemies={inActive}");
            }
            catch (Exception ex)
            {
                Debug.Log($"[EnemyRando]   allEnemies/activeEnemies check failed: {ex.Message}");
            }

            Collider[] colliders = e.GetComponentsInChildren<Collider>(true);
            Debug.Log($"[EnemyRando]   colliders total={colliders.Length}, enabled={colliders.Count(c => c.enabled)}");

            SaveStateKillableEntity saveState = e.GetComponent<SaveStateKillableEntity>();
            if (saveState != null)
            {
                Debug.Log($"[EnemyRando]   saveState.UUID={saveState.UUID}, isDuplicate={saveState.isDuplicate}, killedPreviously={saveState.killedPreviously}");
            }
        }

        static void DumpProp(Traverse t, string propName)
        {
            try
            {
                Debug.Log($"[EnemyRando]   {propName}={t.Property(propName).GetValue()}");
            }
            catch (Exception e)
            {
                Debug.Log($"[EnemyRando]   {propName}=<error: {e.Message}>");
            }
        }

        static void DumpField(Traverse t, string fieldName)
        {
            try
            {
                Debug.Log($"[EnemyRando]   {fieldName}={t.Field(fieldName).GetValue()}");
            }
            catch (Exception e)
            {
                Debug.Log($"[EnemyRando]   {fieldName}=<error: {e.Message}>");
            }
        }
    }
}
