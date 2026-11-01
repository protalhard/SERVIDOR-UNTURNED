using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>/montar safe|negro|cacador [raio]: monta a área em volta do admin (cerca com uma entrada só
    /// na direção pra onde ele olha, NPCs com nome, ATM na safe) e registra a zona/loja. /desmontar desfaz a última.</summary>
    public partial class Plugin
    {
        string ArqMontagens => Path.Combine(Directory, "montagens.tsv");
        HashSet<uint> pecasMontadas;

        /// <summary>Peças do /montar não quebram (cerca do mercado negro fica na zona PvP).</summary>
        public bool PecaMontada(Transform t)
        {
            if (pecasMontadas == null)
            {
                pecasMontadas = new HashSet<uint>();
                try
                {
                    if (File.Exists(ArqMontagens))
                        foreach (var l in File.ReadAllLines(ArqMontagens))
                        {
                            var c = l.Split('\t');
                            if (c.Length > 2) foreach (var x in c[2].Split(',')) if (uint.TryParse(x, out var v)) pecasMontadas.Add(v);
                        }
                }
                catch { }
            }
            var d = BarricadeManager.FindBarricadeByRootTransform(t);
            return d != null && pecasMontadas.Contains(d.instanceID);
        }

        static readonly (ushort npc, string nome)[] NpcsSafe =
        {
            (51240, "Armas"), (51241, "Comida"), (51242, "Roupas"), (51243, "Remedios"),
            (51244, "Equipamentos"), (51245, "Garagem"), (51246, "Vault"), (51249, "ATM"),
        };

        static float Chao(Vector3 pos)
        {
            // pega o chão de verdade (terreno ou objeto), senão a altura do terreno
            var de = new Vector3(pos.x, pos.y + 30f, pos.z);
            if (Physics.Raycast(de, Vector3.down, out var hit, 80f, RayMasks.BLOCK_COLLISION)) return hit.point.y;
            return LevelGround.getHeight(pos);
        }

        static Quaternion Olhando(Vector3 de, Vector3 para)
        {
            var d = para - de; d.y = 0;
            return d.sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(d.normalized);
        }

        uint Colocar(ushort id, Vector3 pos, Quaternion rot, ulong dono)
        {
            if (!(Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset asset)) return 0;
            var t = BarricadeManager.dropNonPlantedBarricade(new Barricade(asset), pos, rot, dono, 0);
            var d = t != null ? BarricadeManager.FindBarricadeByRootTransform(t) : null;
            return d?.instanceID ?? 0;
        }

        public string Montar(UnturnedPlayer adm, string tipo, float raio)
        {
            var centro = adm.Position;
            var frente = adm.Player.transform.forward; frente.y = 0; frente.Normalize();
            float angEntrada = Mathf.Atan2(frente.x, frente.z);
            var colocados = new List<uint>();
            ulong dono = Id(adm);
            int npcs = 0, cerca = 0;

            // cerca em círculo com uma abertura (entrada) na frente do admin
            if (S.MontarCercaId != 0 && Assets.find(EAssetType.ITEM, S.MontarCercaId) is ItemBarricadeAsset)
            {
                float passo = Mathf.Max(1f, S.MontarCercaEspaco);
                int pecas = Mathf.Max(8, Mathf.RoundToInt(2 * Mathf.PI * raio / passo));
                float abertura = Mathf.Max(S.MontarEntrada, passo) / raio; // em radianos
                for (int i = 0; i < pecas; i++)
                {
                    float a = 2 * Mathf.PI * i / pecas;
                    float dif = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, angEntrada * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (dif < abertura / 2f + 0.01f) continue;
                    var pos = centro + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * raio;
                    pos.y = Chao(pos);
                    var rot = Quaternion.Euler(S.MontarRotacaoX, a * Mathf.Rad2Deg, 0f);
                    var id = Colocar(S.MontarCercaId, pos, rot, dono);
                    if (id != 0) { colocados.Add(id); cerca++; }
                }
            }

            // NPCs virados pro centro
            void Npc(ushort id, Vector3 pos)
            {
                pos.y = Chao(pos);
                var rot = Quaternion.Euler(S.MontarRotacaoX, Olhando(pos, centro).eulerAngles.y, 0f);
                var iid = Colocar(id, pos, rot, dono);
                if (iid != 0) { colocados.Add(iid); npcs++; }
            }

            var cfg = Configuration.Instance;
            void Loja(string idLoja)
            {
                var l = cfg.Lojas.FirstOrDefault(x => x.Id == idLoja);
                if (l == null) return;
                l.X = centro.x; l.Y = centro.y; l.Z = centro.z; l.Raio = raio;
                if (idLoja == "garagem" || idLoja == "negro")
                {
                    // veículos aparecem do lado de fora da entrada
                    var v = centro + frente * (raio + 8f);
                    l.VX = v.x; l.VY = Chao(v); l.VZ = v.z;
                }
            }

            switch (tipo)
            {
                case "safe":
                    float r = Mathf.Max(6f, raio * 0.45f);
                    for (int i = 0; i < NpcsSafe.Length; i++)
                    {
                        // espalha os NPCs num arco do lado oposto à entrada
                        float a = angEntrada + Mathf.PI + (i - (NpcsSafe.Length - 1) / 2f) * (Mathf.PI * 1.3f / NpcsSafe.Length);
                        var pos = centro + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r;
                        Npc(NpcsSafe[i].npc, pos);
                        if (NpcsSafe[i].npc == 51249)
                            cfg.Atms.Add(new Ponto("ATM " + (cfg.Atms.Count + 1), 4f) { X = pos.x, Y = Chao(pos), Z = pos.z });
                    }
                    var zona = new Ponto("Safe " + (cfg.ZonasSeguras.Count + 1), raio + 5f) { X = centro.x, Y = centro.y, Z = centro.z };
                    cfg.ZonasSeguras.Add(zona);
                    foreach (var id in new[] { "armas", "comida", "roupas", "remedios", "equipamentos", "garagem" }) Loja(id);
                    break;
                case "negro":
                    Npc(51247, centro - frente * Mathf.Max(3f, raio * 0.4f));
                    Loja("negro");
                    break;
                case "cacador":
                    Npc(51248, centro - frente * Mathf.Max(3f, raio * 0.4f));
                    Loja("cacador");
                    break;
                default:
                    return "Use: /montar safe|negro|cacador [raio]";
            }
            Configuration.Save();
            pecasMontadas = null;
            try { File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\t{tipo}\t{string.Join(",", colocados)}{Environment.NewLine}"); } catch { }
            LogAdmin(adm, $"MONTOU {tipo} (raio {raio}) em {centro.x:0} {centro.z:0}: {cerca} pecas de cerca, {npcs} NPCs");
            string falta = npcs == 0 ? " Os NPCs ainda nao estao no pacote do Workshop: rode de novo depois da atualizacao (so a parte dos NPCs: /montar " + tipo + " 0)." : "";
            return $"Montado: {cerca} pecas de cerca e {npcs} NPCs.{falta} Desfazer: /desmontar";
        }

        public string Desmontar()
        {
            if (!File.Exists(ArqMontagens)) return "Nada pra desfazer.";
            var linhas = File.ReadAllLines(ArqMontagens).Where(l => l.Trim() != "").ToList();
            if (linhas.Count == 0) return "Nada pra desfazer.";
            var ult = linhas.Last().Split('\t');
            var ids = new HashSet<uint>((ult.Length > 2 ? ult[2] : "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => uint.TryParse(x, out var v) ? v : 0).Where(v => v != 0));
            int tirados = 0;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    var reg = BarricadeManager.regions[x, y];
                    foreach (var d in reg.drops.Where(d => ids.Contains(d.instanceID)).ToList())
                    { BarricadeManager.destroyBarricade(d, x, y, ushort.MaxValue); tirados++; }
                }
            linhas.RemoveAt(linhas.Count - 1);
            pecasMontadas = null;
            File.WriteAllLines(ArqMontagens, linhas);
            return $"Desfeito: {tirados} pecas tiradas ({ult[1]}). A zona/loja registrada continua; use /delsafe se precisar.";
        }
    }

    public class CmdMontar : ComandoSistema
    {
        public override string Name => "montar";
        public override string Help => "Monta safe, mercado negro ou cacador em volta de voce (dono).";
        public override string Syntax => "<safe|negro|cacador> [raio]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length < 1) { Chat.Info(p, "Fique no centro, olhe pra onde quer a entrada e use: /montar safe 40 | /montar negro 25 | /montar cacador 20"); return; }
            var tipo = a[0].ToLowerInvariant();
            float padrao = tipo == "safe" ? 40 : tipo == "negro" ? 25 : 20;
            float raio = a.Length > 1 && float.TryParse(a[1], out var r) ? Mathf.Clamp(r, 0, 150) : padrao;
            var salvo = S.MontarCercaId;
            if (raio == 0) { S.MontarCercaId = 0; raio = padrao; } // 0 = só os NPCs
            try { Chat.Ok(p, P.Montar(p, tipo, raio)); }
            finally { S.MontarCercaId = salvo; }
        }
    }

    public class CmdDesmontar : ComandoSistema
    {
        public override string Name => "desmontar";
        public override string Help => "Desfaz o ultimo /montar (dono).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            Chat.Info(p, P.Desmontar());
        }
    }
}
