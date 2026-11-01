using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>/montar safe|negro|cacador 1-3: monta o modelo escolhido em volta do admin (ele fica no centro,
    /// olhando pra onde quer a entrada). Os modelos vêm de montar_modelos.tsv (gerado por modelos/exportar.py;
    /// um arquivo com esse nome na pasta do plugin substitui o embutido). /desmontar desfaz a última.</summary>
    public partial class Plugin
    {
        string ArqMontagens => Path.Combine(Directory, "montagens.tsv");
        HashSet<uint> pecasMontadas;

        class PecaModelo
        {
            public string Tipo, Extra;
            public float X, Y, X2, Y2;
        }

        Dictionary<string, List<PecaModelo>> modelosMontar;

        Dictionary<string, List<PecaModelo>> ModelosMontar()
        {
            if (modelosMontar != null) return modelosMontar;
            string texto = null;
            var arq = Path.Combine(Directory, "montar_modelos.tsv");
            try
            {
                if (File.Exists(arq)) texto = File.ReadAllText(arq);
                else
                    using (var st = typeof(Plugin).Assembly.GetManifestResourceStream("montar_modelos.tsv"))
                    using (var r = new StreamReader(st)) texto = r.ReadToEnd();
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            var res = new Dictionary<string, List<PecaModelo>>();
            foreach (var l in (texto ?? "").Split('\n'))
            {
                if (l.StartsWith("#") || l.Trim() == "") continue;
                var c = l.TrimEnd('\r').Split('\t');
                if (c.Length < 3) continue;
                float F(int i) => c.Length > i && float.TryParse(c[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
                var pc = new PecaModelo { Tipo = c[1], X = F(2), Y = F(3), X2 = F(4), Y2 = F(5), Extra = c.Length > 6 ? c[6] : "" };
                if (!res.TryGetValue(c[0], out var lista)) res[c[0]] = lista = new List<PecaModelo>();
                lista.Add(pc);
            }
            return modelosMontar = res;
        }

        /// <summary>Peças do /montar não quebram e não abrem (enfeite).</summary>
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
            if (pecasMontadas.Count == 0 || t == null) return false;
            var d = BarricadeManager.FindBarricadeByRootTransform(t);
            return d != null && pecasMontadas.Contains(d.instanceID);
        }

        ushort IdPeca(string peca, string area)
        {
            string Achar(string chave)
            {
                foreach (var s in S.MontarPecas ?? new List<string>())
                {
                    var i = s.IndexOf('=');
                    if (i > 0 && s.Substring(0, i).Trim().Equals(chave, StringComparison.OrdinalIgnoreCase)) return s.Substring(i + 1).Trim();
                }
                return null;
            }
            var v = Achar(peca + "@" + area) ?? Achar(peca);
            // "51266|6173": usa a primeira que existe no servidor (peça do pack DZ ou uma do jogo no lugar)
            foreach (var x in (v ?? "").Split('|'))
                if (ushort.TryParse(x.Trim(), out var id) && Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset) return id;
            return 0;
        }

        static float Chao(Vector3 pos)
        {
            // chão de verdade (terreno, estrada, prédio), ignorando barricadas já colocadas
            int mascara = RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.ENVIRONMENT | RayMasks.LARGE | RayMasks.MEDIUM;
            var de = new Vector3(pos.x, pos.y + 25f, pos.z);
            if (Physics.Raycast(de, Vector3.down, out var hit, 60f, mascara)) return hit.point.y;
            return LevelGround.getHeight(pos);
        }

        BarricadeDrop Colocar(ushort id, Vector3 pos, float yaw, ulong dono, bool npc = false)
        {
            if (id == 0 || !(Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset asset)) return null;
            // padrão do Unturned: Euler(-90, yaw, 0) fica em pé virado pro yaw; a origem fica "offset" acima do chão
            var rot = Quaternion.Euler(npc ? S.MontarRotacaoXNpc : S.MontarRotacaoX, yaw, 0f);
            pos.y += asset.offset;
            var t = BarricadeManager.dropNonPlantedBarricade(new Barricade(asset), pos, rot, dono, 0);
            return t != null ? BarricadeManager.FindBarricadeByRootTransform(t) : null;
        }

        /// <summary>Itens pra expor: "loja" ou "loja:Categoria,Categoria".</summary>
        List<ushort> ItensExpostos(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return new List<ushort>();
            var partes = spec.Split(':');
            var loja = Configuration.Instance.Lojas.FirstOrDefault(l => l.Id == partes[0]);
            if (loja == null) return new List<ushort>();
            var cats = partes.Length > 1 ? partes[1].Split(',') : null;
            return loja.Itens.Where(i => !i.Veiculo && (cats == null || cats.Contains(i.Categoria)) && Itens.Existe(i.Id))
                .Select(i => i.Id).Distinct().ToList();
        }

        public string Montar(UnturnedPlayer adm, string area, int modelo)
        {
            if (!ModelosMontar().TryGetValue(area + modelo, out var pecas)) return "Modelo nao existe. Use /montar safe|negro|cacador 1|2|3";
            var faltaNpc = pecas.Where(x => x.Tipo == "npc" && ushort.TryParse(x.Extra, out var n) && !(Assets.find(EAssetType.ITEM, n) is ItemBarricadeAsset))
                .Select(x => x.Extra).Distinct().ToList();
            if (faltaNpc.Count > 0) return "Os NPCs " + string.Join(", ", faltaNpc) + " ainda nao estao no mod DZ Menu. Atualize o mod no Workshop e reinicie antes de montar.";

            var centro = adm.Position;
            var frente = adm.Player.transform.forward; frente.y = 0; frente.Normalize();
            var direita = new Vector3(frente.z, 0, -frente.x);
            Vector3 Mundo(float x, float y) { var v = centro - direita * x - frente * y; v.y = Chao(v); return v; }
            float Yaw(float fx, float fy)
            {
                var w = -direita * fx - frente * fy;
                return w.sqrMagnitude < 1e-4f ? adm.Player.transform.eulerAngles.y : Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg;
            }

            var cfg = Configuration.Instance;
            var colocados = new List<uint>();
            ulong dono = Id(adm);
            int nPecas = 0, nNpcs = 0, nExpostos = 0;
            var expostos = new Dictionary<string, int>();
            void Guardar(BarricadeDrop d) { if (d != null) { colocados.Add(d.instanceID); nPecas++; } }

            foreach (var pc in pecas)
            {
                try
                {
                    switch (pc.Tipo)
                    {
                        case "zona":
                            if (area == "safe")
                                cfg.ZonasSeguras.Add(new Ponto("Safe " + (cfg.ZonasSeguras.Count + 1), pc.X) { X = centro.x, Y = centro.y, Z = centro.z });
                            break;
                        case "cerca":
                        case "parede":
                        {
                            ushort id = IdPeca(pc.Tipo, area);
                            if (id == 0) break;
                            float comp = Mathf.Sqrt((pc.X2 - pc.X) * (pc.X2 - pc.X) + (pc.Y2 - pc.Y) * (pc.Y2 - pc.Y));
                            float esp = Mathf.Max(0.5f, pc.Tipo == "cerca" ? S.MontarEspacoCerca : S.MontarEspacoParede);
                            // cerca: a largura da peça é ~2x o raio dela (7122/7121 = 3 m, 7120 = 1,6 m)
                            if (pc.Tipo == "cerca" && Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset ca && ca.radius > 0.3f) esp = ca.radius * 2f;
                            int n = Mathf.Max(1, Mathf.RoundToInt(comp / esp));
                            // virada perpendicular à linha (a largura da peça fica ao longo da linha)
                            float yaw = Yaw(-(pc.Y2 - pc.Y), pc.X2 - pc.X) + S.MontarGiroParede;
                            for (int i = 0; i < n; i++)
                            {
                                float t = (i + .5f) / n;
                                Guardar(Colocar(id, Mundo(pc.X + (pc.X2 - pc.X) * t, pc.Y + (pc.Y2 - pc.Y) * t), yaw, dono));
                            }
                            break;
                        }
                        case "npc":
                        {
                            if (!ushort.TryParse(pc.Extra, out var id)) break;
                            var pos = Mundo(pc.X, pc.Y);
                            var d = Colocar(id, pos, Yaw(pc.X2, pc.Y2), dono, true);
                            if (d == null) break;
                            Guardar(d); nNpcs++;
                            if (id == 51249) cfg.Atms.Add(new Ponto("ATM " + (cfg.Atms.Count + 1), 4f) { X = pos.x, Y = pos.y, Z = pos.z });
                            break;
                        }
                        case "veiculo":
                        {
                            // carro comprado na garagem da safe nasce aqui
                            var v = Mundo(pc.X, pc.Y);
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == "garagem");
                            if (loja != null && area == "safe") { loja.VX = v.x; loja.VY = v.y; loja.VZ = v.z; }
                            break;
                        }
                        case "vaga":
                        {
                            var v = Mundo(pc.X, pc.Y); float r = Yaw(pc.X2, pc.Y2);
                            Guardar(Colocar(IdPeca("vaga", area), v, r, dono));
                            (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "carro", X = v.x, Y = v.y, Z = v.z, R = r });
                            break;
                        }
                        case "heliponto":
                        {
                            var v = Mundo(pc.X, pc.Y);
                            Guardar(Colocar(IdPeca("heliponto", area), v, Yaw(0, 1), dono));
                            // safe e negro: heli da garagem sai aqui (no caçador é só pouso)
                            if (area != "cacador") (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "heli", X = v.x, Y = v.y, Z = v.z, R = Yaw(0, 1) });
                            // no mercado negro o heli comprado nasce no heliponto
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == (area == "safe" ? "hangar" : "negro"));
                            if (loja != null && (area == "negro" || area == "safe")) { loja.VX = v.x; loja.VY = v.y; loja.VZ = v.z; }
                            break;
                        }
                        case "pista_inicio":
                        {
                            var v = Mundo(pc.X, pc.Y);
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == (area == "safe" ? "hangar" : "negro"));
                            if (loja != null && (area == "negro" || area == "safe")) { loja.AX = v.x; loja.AY = v.y; loja.AZ = v.z; loja.AR = Yaw(pc.X2, pc.Y2); }
                            (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "aviao", X = v.x, Y = v.y, Z = v.z, R = Yaw(pc.X2, pc.Y2) });
                            break;
                        }
                        case "bandeira":
                        {
                            var pos = Mundo(pc.X, pc.Y); float yaw = Yaw(pc.X2, pc.Y2);
                            var d = Colocar(IdPeca("bandeira", area), pos, yaw, dono);
                            if (d == null)
                            {
                                d = Colocar(IdPeca("bandeira_placa", area), pos, yaw, dono);
                                if (d?.interactable is InteractableSign placa) BarricadeManager.ServerSetSignText(placa, "DZ BRASIL");
                            }
                            Guardar(d);
                            break;
                        }
                        default:
                        {
                            var d = Colocar(IdPeca(pc.Tipo, area), Mundo(pc.X, pc.Y), Yaw(pc.X2, pc.Y2), dono);
                            if (d == null) break;
                            Guardar(d);
                            if (d.interactable is InteractableFire fogo) BarricadeManager.ServerSetFireLit(fogo, true);
                            if (d.interactable is InteractableStorage bau && !string.IsNullOrEmpty(pc.Extra))
                            {
                                // enfeite: coloca um item da loja no mostruário (ninguém consegue abrir)
                                var lista = ItensExpostos(pc.Extra);
                                if (lista.Count == 0) break;
                                expostos.TryGetValue(pc.Extra, out var k);
                                expostos[pc.Extra] = k + 1;
                                var item = new Item(lista[k % lista.Count], EItemOrigin.ADMIN);
                                if (bau.items.tryAddItem(item))
                                {
                                    nExpostos++;
                                    if (bau.isDisplay) BarricadeManager.sendStorageDisplay(d.model, item, 0, 0, "", "");
                                    bau.rebuildState();
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }

            // lojas da área apontam pro centro (o NPC mais perto do tipo abre a loja)
            var ids = area == "safe" ? new[] { "armas", "comida", "roupas", "remedios", "equipamentos", "garagem", "construcao" }
                : new[] { area };
            foreach (var idLoja in ids)
            {
                var l = cfg.Lojas.FirstOrDefault(x => x.Id == idLoja);
                if (l == null) continue;
                l.X = centro.x; l.Y = centro.y; l.Z = centro.z;
                if (l.Raio < 30) l.Raio = 30;
            }
            Configuration.Save();
            pecasMontadas = null;
            try
            {
                File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\t{area}{modelo}\t{string.Join(",", colocados)}\t" +
                    $"{centro.x:0.0} {centro.y:0.0} {centro.z:0.0}{Environment.NewLine}");
            }
            catch { }
            LogAdmin(adm, $"MONTOU {area} modelo {modelo} em {centro.x:0} {centro.z:0}: {nPecas} pecas, {nNpcs} NPCs, {nExpostos} itens expostos");
            return $"Montado {area} modelo {modelo}: {nPecas} pecas ({nNpcs} NPCs, {nExpostos} itens nos mostruarios). Desfazer: /desmontar";
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
                    {
                        // esvazia antes pra não cair item no chão
                        if (d.interactable is InteractableStorage bau) bau.items.clear();
                        BarricadeManager.destroyBarricade(d, x, y, ushort.MaxValue); tirados++;
                    }
                }
            linhas.RemoveAt(linhas.Count - 1);
            pecasMontadas = null;
            File.WriteAllLines(ArqMontagens, linhas);
            return $"Desfeito: {tirados} pecas tiradas ({ult[1]}). A zona/ATM registrados continuam; use /delsafe se precisar.";
        }
    }

    public class CmdMontar : ComandoSistema
    {
        public override string Name => "montar";
        public override string Help => "Monta safe, mercado negro ou cacador em volta de voce (dono).";
        public override string Syntax => "<safe|negro|cacador> <1|2|3>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length < 1)
            {
                Chat.Info(p, "Fique no centro, olhe pra onde quer a ENTRADA e use: /montar safe 1 | /montar negro 2 | /montar cacador 3");
                return;
            }
            var area = a[0].ToLowerInvariant();
            if (area == "caca" || area == "caçador") area = "cacador";
            if (area == "mercadonegro") area = "negro";
            int modelo = a.Length > 1 && int.TryParse(a[1], out var m) ? m : 1;
            P.ModelosRecarregar();
            Chat.Ok(p, P.Montar(p, area, modelo));
        }
    }

    public partial class Plugin
    {
        public void ModelosRecarregar() => modelosMontar = null;
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
