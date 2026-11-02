using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Ponto de spawn dentro do ônibus de uma safe do mapa (o dono grava com /safespawn add).</summary>
    public class SpawnSafe
    {
        [XmlAttribute] public string Nome;
        [XmlAttribute] public float X;
        [XmlAttribute] public float Y;
        [XmlAttribute] public float Z;
        [XmlAttribute] public float R;
    }

    /// <summary>Spawn nas safes do mapa que têm ônibus: jogador novo nasce numa delas, quem morre renasce numa delas,
    /// e ao entrar/renascer pode escolher pra qual safe ir (por um tempo). O dono grava os pontos dentro de cada ônibus.</summary>
    public partial class Plugin
    {
        const ushort SpawnUiId = 51296; const short SpawnUiKey = 5129;
        readonly Dictionary<ulong, float> escolherSafeAte = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, bool> spawnAberto = new Dictionary<ulong, bool>();
        // última safe escolhida e quantas vezes seguidas: repetir a mesma pede 1 min, depois 2, 3...; escolher outra zera
        class UltimaSafe { public string Nome; public int Seguidas; public float Quando; }
        readonly Dictionary<ulong, UltimaSafe> ultimaSafe = new Dictionary<ulong, UltimaSafe>();

        /// <summary>Segundos que faltam pra poder escolher essa safe de novo (0 = livre).</summary>
        int EsperaSafe(ulong id, string nome)
        {
            if (!ultimaSafe.TryGetValue(id, out var u) || !u.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)) return 0;
            return Mathf.Max(0, Mathf.CeilToInt(u.Quando + u.Seguidas * 60f - Time.realtimeSinceStartup));
        }

        List<SpawnSafe> Spawns => Configuration.Instance.SpawnsSafe ?? (Configuration.Instance.SpawnsSafe = new List<SpawnSafe>());

        /// <summary>Nomes das safes com spawn, na ordem em que foram gravadas.</summary>
        public List<string> SafesComSpawn() => Spawns.Select(s => s.Nome).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        SpawnSafe SpawnDa(string nome)
        {
            var l = Spawns.Where(s => s.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)).ToList();
            return l.Count == 0 ? null : l[UnityEngine.Random.Range(0, l.Count)];
        }

        SpawnSafe SpawnAleatorio()
        {
            var nomes = SafesComSpawn();
            return nomes.Count == 0 ? null : SpawnDa(nomes[UnityEngine.Random.Range(0, nomes.Count)]);
        }

        /// <summary>Acha a safe pelo número da lista (1, 2, 3...) ou pelo nome.</summary>
        public string AcharSafe(string arg)
        {
            var nomes = SafesComSpawn();
            if (int.TryParse(arg, out var n) && n >= 1 && n <= nomes.Count) return nomes[n - 1];
            return nomes.FirstOrDefault(x => x.Equals(arg, StringComparison.OrdinalIgnoreCase))
                ?? nomes.FirstOrDefault(x => x.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void IrPraSafe(UnturnedPlayer p, string nome)
        {
            var s = SpawnDa(nome);
            if (s == null || p?.Player == null) return;
            GuardLiberar(Id(p), 8f);
            p.Player.teleportToLocationUnsafe(new Vector3(s.X, s.Y + .3f, s.Z), s.R);
        }

        // ---------- Entrar e renascer ----------

        void SpawnAoConectar(UnturnedPlayer p, bool novo)
        {
            // só na primeira vez no servidor: quem só reconectou (ou voltou de um reinício) fica onde estava
            if (!novo || SafesComSpawn().Count == 0) return;
            StartCoroutine(Depois(2f, p, () => { var s = SpawnAleatorio(); if (s != null) IrPraSafe(p, s.Nome); }));
            StartCoroutine(Depois(5f, p, () => AbrirEscolhaSafe(p, false)));
        }

        void SpawnAoRenascer(UnturnedPlayer p, Vector3 pos)
        {
            if (SafesComSpawn().Count == 0) return;
            // renasceu na cama: fica lá
            if (BarricadeManager.tryGetBed(p.CSteamID, out var cama, out _) && (cama - pos).sqrMagnitude < 25f) return;
            StartCoroutine(Depois(.5f, p, () => { var s = SpawnAleatorio(); if (s != null) IrPraSafe(p, s.Nome); }));
            StartCoroutine(Depois(2f, p, () => AbrirEscolhaSafe(p, true)));
        }

        /// <summary>Mostra a escolha de safe (janela se o mod tiver, senão no chat). ficar = mostra "continuar onde estou".</summary>
        public void AbrirEscolhaSafe(UnturnedPlayer p, bool ficar)
        {
            var nomes = SafesComSpawn();
            if (nomes.Count == 0 || p?.Player == null || p.Dead) return;
            escolherSafeAte[Id(p)] = Time.realtimeSinceStartup + Math.Max(30, S.EscolherSafeSegundos);
            if (Assets.find(EAssetType.EFFECT, SpawnUiId) is EffectAsset)
            {
                var c = p.Player.channel.owner.transportConnection;
                EffectManager.sendUIEffect(SpawnUiId, SpawnUiKey, c, true);
                EffectManager.sendUIEffectText(SpawnUiKey, c, true, "Spawn_Titulo", "ESCOLHA ONDE NASCER");
                EffectManager.sendUIEffectVisibility(SpawnUiKey, c, true, "Spawn_Ficar", ficar);
                for (int i = 0; i < 8; i++)
                {
                    bool tem = i < nomes.Count;
                    EffectManager.sendUIEffectVisibility(SpawnUiKey, c, true, "Spawn_Safe_" + (i + 1), tem);
                    if (!tem) continue;
                    int gente = Provider.clients.Count(sp => sp?.player != null && Spawns.Any(s => s.Nome.Equals(nomes[i], StringComparison.OrdinalIgnoreCase)
                        && (sp.player.transform.position - new Vector3(s.X, s.Y, s.Z)).sqrMagnitude < 150f * 150f));
                    float dist = Vector3.Distance(p.Position, new Vector3(SpawnDa(nomes[i]).X, p.Position.y, SpawnDa(nomes[i]).Z));
                    EffectManager.sendUIEffectText(SpawnUiKey, c, true, "Spawn_Safe_" + (i + 1) + "_Nome", Chat.Limpar(nomes[i]).ToUpperInvariant());
                    int espera = EsperaSafe(Id(p), nomes[i]);
                    EffectManager.sendUIEffectText(SpawnUiKey, c, true, "Spawn_Safe_" + (i + 1) + "_Info", espera > 0
                        ? $"<color=#FF5A5A>liberada em {espera / 60}:{espera % 60:00}</color> · escolha outra"
                        : $"{gente} jogando aqui · {dist / 1000f:0.0} km");
                }
                p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
                spawnAberto[Id(p)] = true;
                return;
            }
            Chat.Para(p, Chat.Cor(S.CorDestaque, Chat.Forte("ESCOLHA SUA SAFE: ")) + Chat.Cor(S.CorTexto, $"voce tem {Math.Max(30, S.EscolherSafeSegundos)}s"));
            for (int i = 0; i < nomes.Count; i++)
            {
                int espera = EsperaSafe(Id(p), nomes[i]);
                Chat.Para(p, Chat.Cor(S.CorPrincipal, Chat.Forte($"/irsafe {i + 1}")) + Chat.Cor(S.CorTexto, "  " + Chat.Limpar(nomes[i]))
                    + (espera > 0 ? Chat.Cor("#FF5A5A", $"  (liberada em {espera / 60}:{espera % 60:00})") : ""));
            }
            if (ficar) Chat.Para(p, Chat.Cor(S.CorApagada, "Ou fique onde esta (nao precisa digitar nada)."));
        }

        public void FecharEscolhaSafe(UnturnedPlayer p)
        {
            if (!spawnAberto.Remove(Id(p))) return;
            EffectManager.askEffectClearByID(SpawnUiId, p.Player.channel.owner.transportConnection);
            if (!MenuAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        /// <summary>Jogador escolheu uma safe (botão ou /irsafe). Só vale logo depois de entrar ou renascer.</summary>
        public void EscolherSafe(UnturnedPlayer p, string nome)
        {
            FecharEscolhaSafe(p);
            if (!escolherSafeAte.TryGetValue(Id(p), out var ate) || Time.realtimeSinceStartup > ate)
            { Chat.Erro(p, "A escolha de safe so vale logo depois de entrar no servidor ou renascer."); return; }
            if (EmCombate(p, out var resta)) { Chat.Erro(p, $"Voce esta em combate. Espere {resta}s."); return; }
            int espera = EsperaSafe(Id(p), nome);
            if (espera > 0)
            {
                Chat.Erro(p, $"Voce acabou de ir pra {Chat.Limpar(nome)}. Ela libera em {espera / 60}:{espera % 60:00}. Escolha outra safe.");
                AbrirEscolhaSafe(p, true);
                return;
            }
            if (ultimaSafe.TryGetValue(Id(p), out var u) && u.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)) u.Seguidas++;
            else ultimaSafe[Id(p)] = u = new UltimaSafe { Nome = nome, Seguidas = 1 };
            u.Quando = Time.realtimeSinceStartup;
            escolherSafeAte.Remove(Id(p));
            IrPraSafe(p, nome);
            Chat.Ok(p, "Voce foi pra safe " + Chat.Limpar(nome) + ".");
        }

        bool CliqueSpawn(UnturnedPlayer p, string botao)
        {
            if (!spawnAberto.ContainsKey(Id(p))) return false;
            if (botao == "Spawn_Ficar" || botao == "Spawn_Fechar") { FecharEscolhaSafe(p); escolherSafeAte.Remove(Id(p)); return true; }
            if (botao.StartsWith("Spawn_Safe_") && int.TryParse(botao.Substring(11), out var n))
            {
                var nomes = SafesComSpawn();
                if (n >= 1 && n <= nomes.Count) EscolherSafe(p, nomes[n - 1]);
                return true;
            }
            return false;
        }

        // ---------- /itens: peças de um modelo do /montar pro inventário do dono ----------

        readonly Dictionary<ulong, List<ushort>> itensPendentes = new Dictionary<ulong, List<ushort>>();

        public string DarItensModelo(UnturnedPlayer p, string area, int modelo)
        {
            var modelos = ModelosMontar();
            if (!modelos.TryGetValue(area + modelo, out var pecas)) return $"Nao achei o modelo {area} {modelo}.";
            var lista = new List<ushort>();
            foreach (var pc in pecas)
            {
                ushort id = 0; int qtd = 1;
                switch (pc.Tipo)
                {
                    case "zona": case "veiculo": case "pista_inicio": continue;
                    case "npc": ushort.TryParse(pc.Extra, out id); break;
                    case "cerca":
                    case "parede":
                    {
                        if (area == "safe") continue; // na safe vem só 6 por NPC, pra separar as lojas (abaixo)
                        id = IdPeca(pc.Tipo, area);
                        float esp = Mathf.Max(.5f, pc.Tipo == "cerca" ? S.MontarEspacoCerca : S.MontarEspacoParede);
                        if (Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset ba && ba.radius > .3f) esp = ba.radius * 2f;
                        qtd = Mathf.Max(1, Mathf.RoundToInt(new Vector2(pc.X2 - pc.X, pc.Y2 - pc.Y).magnitude / esp));
                        break;
                    }
                    default: id = IdPeca(pc.Tipo, area); break;
                }
                if (id == 0 || !Itens.Existe(id)) continue;
                for (int i = 0; i < qtd; i++) lista.Add(id);
            }
            if (area == "safe")
            {
                int npcs = pecas.Count(pc => pc.Tipo == "npc");
                ushort cerca = IdPeca("cerca", area);
                if (cerca == 0 || !Itens.Existe(cerca)) cerca = IdPeca("parede", area);
                if (cerca != 0 && Itens.Existe(cerca)) for (int i = 0; i < 6 * npcs; i++) lista.Add(cerca);
                // terminal da garagem (tira/guarda carro) e 4 placas de vaga; as vagas se gravam com /vaga add
                if (Itens.Existe(TerminalId)) lista.Add(TerminalId);
                var placaVaga = IdPeca("vaga", area);
                if (placaVaga != 0 && Itens.Existe(placaVaga)) for (int i = 0; i < 4; i++) lista.Add(placaVaga);
            }
            if (lista.Count == 0) return "Esse modelo nao tem pecas.";
            // mochila grande primeiro (se ele estiver sem mochila, já veste)
            ushort mochila = MaiorMochila();
            if (mochila != 0 && Assets.find(EAssetType.ITEM, mochila) is ItemBackpackAsset mb)
            {
                if (p.Player.clothing.backpack == 0) p.Player.clothing.askWearBackpack(mochila, 100, mb.getState(), true);
                else p.Inventory.forceAddItem(new Item(mochila, true), true);
            }
            itensPendentes[Id(p)] = lista;
            return $"{area} {modelo}: {lista.Count} pecas. " + EntregarPendentes(p);
        }

        /// <summary>A maior mochila do jogo (mais espaço), pra caber as peças da safe.</summary>
        ushort MaiorMochila()
        {
            var todas = new List<ItemBackpackAsset>();
            Assets.find(todas);
            var maior = todas.Where(m => m != null && m.id != 0 && m.id < 51000 && !m.isPro && !NomeProibido.IsMatch(m.itemName ?? ""))
                .OrderByDescending(m => m.width * m.height).ThenBy(m => m.id).FirstOrDefault();
            return maior?.id ?? S.MochilaItens;
        }

        /// <summary>/por tirar: tira a peça que você está olhando (só peça colocada pelo dono) e devolve pro inventário.</summary>
        public string TirarNaMira(UnturnedPlayer p)
        {
            var olho = p.Player.look.aim;
            if (!Physics.Raycast(olho.position, olho.forward, out var hit, 12f, RayMasks.BARRICADE | RayMasks.STRUCTURE)) return "Olhe pra peca (ate 12 m).";
            var bd = BarricadeManager.FindBarricadeByRootTransform(hit.transform.root) ?? BarricadeManager.FindBarricadeByRootTransform(hit.transform);
            if (bd != null && BarricadeManager.tryGetRegion(bd.model, out var bx, out var by, out var plant, out _))
            {
                if (bd.GetServersideData()?.owner != Id(p) && !EhDono(bd.GetServersideData()?.owner ?? 0)) return "Essa peca nao foi colocada por voce.";
                var id = bd.asset.id;
                if (bd.interactable is InteractableStorage bau) bau.items.clear();
                BarricadeManager.destroyBarricade(bd, bx, by, plant);
                pecasMontadas = null;
                p.Inventory.forceAddItem(new Item(id, true), true);
                return "Tirado e devolvido pro inventario.";
            }
            var sd = StructureManager.FindStructureByRootTransform(hit.transform.root) ?? StructureManager.FindStructureByRootTransform(hit.transform);
            if (sd != null && StructureManager.tryGetRegion(sd.model, out var sx, out var sy, out _))
            {
                if (sd.GetServersideData()?.owner != Id(p) && !EhDono(sd.GetServersideData()?.owner ?? 0)) return "Essa peca nao foi colocada por voce.";
                var id = sd.asset.id;
                StructureManager.destroyStructure(sd, sx, sy, Vector3.zero);
                p.Inventory.forceAddItem(new Item(id, true), true);
                return "Tirado e devolvido pro inventario.";
            }
            return "Nao achei peca ai.";
        }

        /// <summary>/por registrar [raio]: marca como peça da safe tudo que o dono já colocou em volta (bomba enche, sem decay, sem dano).</summary>
        public string RegistrarPecas(UnturnedPlayer p, float raio)
        {
            var c = p.Position; float r2 = raio * raio;
            PecaMontada(null);
            var novos = new List<uint>();
            foreach (var reg in BarricadeManager.regions)
                if (reg?.drops != null)
                    foreach (var d in reg.drops)
                    {
                        var dd = d?.GetServersideData();
                        if (d?.model == null || dd == null || pecasMontadas.Contains(dd.instanceID)) continue;
                        float dx = d.model.position.x - c.x, dz = d.model.position.z - c.z;
                        if (dx * dx + dz * dz < r2 && (dd.owner == Id(p) || EhDono(dd.owner))) novos.Add(dd.instanceID);
                    }
            if (novos.Count == 0) return "Nada novo pra registrar aqui (tudo ja esta registrado).";
            try { System.IO.File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\tpor\t{string.Join(",", novos)}\t{c.x:0.0} {c.y:0.0} {c.z:0.0}{Environment.NewLine}"); }
            catch (Exception e) { return "Erro ao salvar: " + e.Message; }
            pecasMontadas = null;
            return $"{novos.Count} pecas registradas como da safe (nao somem, nao quebram, bomba sempre cheia).";
        }

        public string EntregarPendentes(UnturnedPlayer p)
        {
            if (!itensPendentes.TryGetValue(Id(p), out var lista) || lista.Count == 0) return "Nao tem mais pecas pra entregar.";
            int dadas = 0;
            while (lista.Count > 0 && p.Inventory.tryAddItem(new Item(lista[0], true), true)) { lista.RemoveAt(0); dadas++; }
            if (lista.Count == 0) { itensPendentes.Remove(Id(p)); return $"Entreguei {dadas}. Pronto, foram todas."; }
            return $"Entreguei {dadas}. Faltam {lista.Count}: coloque as que tem e use /itens mais.";
        }

        // ---------- /por: o dono coloca a barricada/estrutura que está na mão onde está olhando ----------

        static bool EhPiso(ItemStructureAsset a) => a.construct == EConstruct.FLOOR || a.construct == EConstruct.FLOOR_POLY;

        /// <summary>Acha o piso já colocado mais perto do ponto mirado e devolve a posição do lado dele (frente, trás, esquerda ou direita).</summary>
        (bool ok, Vector3 pos, Quaternion rot, string erro) EncaixePiso(Vector3 mira)
        {
            StructureDrop perto = null; float melhor = float.MaxValue;
            foreach (var reg in StructureManager.regions)
                if (reg?.drops != null)
                    foreach (var d in reg.drops)
                    {
                        if (d?.model == null || !(d.asset is ItemStructureAsset a) || !EhPiso(a)) continue;
                        var dp = d.model.position;
                        if (Mathf.Abs(dp.y - mira.y) > 3f) continue;
                        float dist = new Vector2(dp.x - mira.x, dp.z - mira.z).sqrMagnitude;
                        if (dist < melhor) { melhor = dist; perto = d; }
                    }
            if (perto == null || melhor > 8f * 8f) return (false, mira, Quaternion.identity, null);
            float yawP = perto.model.rotation.eulerAngles.y;
            var dir = Quaternion.Euler(0f, yawP, 0f);
            Vector3 eixoX = dir * Vector3.right, eixoZ = dir * Vector3.forward;
            // tamanho do piso vizinho nos dois eixos
            var cols = perto.model.GetComponentsInChildren<Collider>(true).Where(k => !k.isTrigger).ToList();
            if (cols.Any(k => k.gameObject.layer == LayerMasks.STRUCTURE)) cols = cols.Where(k => k.gameObject.layer == LayerMasks.STRUCTURE).ToList();
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var col in cols)
                foreach (var v in Cantos(col))
                {
                    float cx = Vector3.Dot(v, eixoX), cz = Vector3.Dot(v, eixoZ);
                    x0 = Mathf.Min(x0, cx); x1 = Mathf.Max(x1, cx); z0 = Mathf.Min(z0, cz); z1 = Mathf.Max(z1, cz);
                }
            float lx = x1 - x0, lz = z1 - z0;
            if (lx < .5f || lx > 20f || lz < .5f || lz > 20f) return (false, mira, Quaternion.identity, null);
            var centro = perto.model.position;
            var rel = mira - centro;
            float px = Vector3.Dot(rel, eixoX) / lx, pz = Vector3.Dot(rel, eixoZ) / lz;
            var passo = Mathf.Abs(px) >= Mathf.Abs(pz) ? eixoX * Mathf.Sign(px) * lx : eixoZ * Mathf.Sign(pz) * lz;
            var novo = centro + passo;
            // já tem piso nesse lugar?
            foreach (var reg in StructureManager.regions)
                if (reg?.drops != null)
                    foreach (var d in reg.drops)
                        if (d?.model != null && d.asset is ItemStructureAsset a2 && EhPiso(a2) && (d.model.position - novo).sqrMagnitude < 1f)
                            return (false, novo, perto.model.rotation, "Ja tem piso desse lado. Mire no lado livre do piso.");
            return (true, novo, perto.model.rotation, null);
        }

        public string PorNaMira(UnturnedPlayer p, float giro)
        {
            var eq = p.Player.equipment;
            if (eq?.asset == null) return "Segure na mao a peca que quer colocar.";
            var olho = p.Player.look.aim;
            if (!Physics.Raycast(olho.position, olho.forward, out var hit, 12f, RayMasks.BLOCK_COLLISION)) return "Olhe pro chao (ate 12 m).";
            float yaw = p.Player.look.yaw + 180f + giro;
            bool ok;
            if (eq.asset is ItemStructureAsset sa)
            {
                // piso: encaixa do lado do piso mais perto (mesma altura e giro), igual a grade normal do jogo
                var pos = hit.point; var rot = Quaternion.Euler(-90f, yaw, 0f);
                if (EhPiso(sa))
                {
                    var enc = EncaixePiso(hit.point);
                    if (enc.erro != null) return enc.erro;
                    if (enc.ok) { pos = enc.pos; rot = enc.rot; }
                }
                ok = StructureManager.dropReplicatedStructure(new Structure(sa, sa.health), pos, rot, Id(p), p.Player.quests.groupID.m_SteamID);
            }
            else if (eq.asset is ItemBarricadeAsset ba)
            {
                // igual ao /montar: a origem da barricada fica "offset" acima do chão (senão o NPC afunda)
                var pos = hit.point + Vector3.up * ba.offset;
                float rotX = EhNpc(ba.id) ? S.MontarRotacaoXNpc : S.MontarRotacaoX;
                var tr = BarricadeManager.dropNonPlantedBarricade(new Barricade(ba), pos, Quaternion.Euler(rotX, yaw, 0f), Id(p), EhNpc(ba.id) ? 0 : p.Player.quests.groupID.m_SteamID);
                ok = tr != null;
                // vira "peça montada": não some pelo tempo, ninguém quebra e bomba de gasolina fica sempre cheia
                var dropPor = ok ? BarricadeManager.FindBarricadeByRootTransform(tr) : null;
                if (dropPor != null)
                    try { System.IO.File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\tpor\t{dropPor.instanceID}\t{pos.x:0.0} {pos.y:0.0} {pos.z:0.0}{Environment.NewLine}"); }
                    catch { }
                if (ok)
                {
                    pecasMontadas = null;
                    // placa de vaga ou heliponto colocados à mão já viram vaga da garagem
                    string tipoVaga = ba.id == IdPeca("vaga", "safe") ? "carro" : ba.id == IdPeca("heliponto", "safe") ? "heli" : null;
                    if (tipoVaga != null)
                    {
                        var cfg = Configuration.Instance;
                        (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = tipoVaga, X = hit.point.x, Y = hit.point.y, Z = hit.point.z, R = p.Player.look.yaw });
                        Configuration.Save();
                        return $"Colocado e gravado como vaga de {tipoVaga}.";
                    }
                }
            }
            else return "Isso nao e uma peca de construcao.";
            if (!ok) return "O jogo nao deixou colocar aqui.";
            // gasta a peça da mão
            byte pg = eq.equippedPage, x = eq.equipped_x, y = eq.equipped_y;
            var idx = p.Player.inventory.getIndex(pg, x, y);
            if (idx != byte.MaxValue) p.Player.inventory.removeItem(pg, idx);
            return "Colocado.";
        }
    }

    public class CmdSafeSpawn : ComandoSistema
    {
        public override string Name => "safespawn";
        public override string Help => "Grava o spawn dentro do onibus de uma safe do mapa (dono).";
        public override string Syntax => "<add Nome da safe | del | list>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            var cfg = P.Configuration.Instance;
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "list";
            if (sub == "add" && a.Length > 1)
            {
                var nome = string.Join(" ", a.Skip(1)).Trim().Trim('"');
                var existente = P.SafesComSpawn().FirstOrDefault(x => x.Equals(nome, StringComparison.OrdinalIgnoreCase));
                if (existente != null) nome = existente;
                (cfg.SpawnsSafe = cfg.SpawnsSafe ?? new List<SpawnSafe>()).Add(new SpawnSafe { Nome = nome, X = p.Position.x, Y = p.Position.y, Z = p.Position.z, R = p.Rotation });
                P.Configuration.Save();
                int n = cfg.SpawnsSafe.Count(s => s.Nome == nome);
                Chat.Ok(p, $"Spawn gravado na safe '{nome}' ({n} ponto{(n > 1 ? "s" : "")} nela). Pode gravar mais de um no mesmo onibus.");
                return;
            }
            if (sub == "del")
            {
                var perto = (cfg.SpawnsSafe ?? new List<SpawnSafe>()).OrderBy(s => (new Vector3(s.X, s.Y, s.Z) - p.Position).sqrMagnitude).FirstOrDefault();
                if (perto == null || (new Vector3(perto.X, perto.Y, perto.Z) - p.Position).sqrMagnitude > 25f) { Chat.Erro(p, "Nao tem spawn a menos de 5 m de voce."); return; }
                cfg.SpawnsSafe.Remove(perto);
                P.Configuration.Save();
                Chat.Ok(p, $"Spawn apagado da safe '{perto.Nome}'.");
                return;
            }
            var nomes = P.SafesComSpawn();
            if (nomes.Count == 0) { Chat.Info(p, "Nenhuma safe gravada. Entre no onibus da safe e use /safespawn add Nome da safe"); return; }
            for (int i = 0; i < nomes.Count; i++)
                Chat.Info(p, $"{i + 1}. {nomes[i]} ({cfg.SpawnsSafe.Count(s => s.Nome == nomes[i])} pontos)");
        }
    }

    public class CmdTpSafe : ComandoSistema
    {
        public override string Name => "tpsafe";
        public override string Help => "Teleporta pra uma safe do mapa (dono/staff).";
        public override string Syntax => "<numero ou nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            var nomes = P.SafesComSpawn();
            if (a.Length == 0)
            {
                if (nomes.Count == 0) { Chat.Info(p, "Nenhuma safe gravada ainda (/safespawn add Nome)."); return; }
                for (int i = 0; i < nomes.Count; i++) Chat.Info(p, $"/tpsafe {i + 1}  {nomes[i]}");
                return;
            }
            var nome = P.AcharSafe(string.Join(" ", a));
            if (nome == null) { Chat.Erro(p, "Safe nao encontrada. Use /tpsafe pra ver a lista."); return; }
            P.IrPraSafe(p, nome);
            Chat.Ok(p, "Safe " + nome + ".");
        }
    }

    public class CmdIrSafe : ComandoSistema
    {
        public override string Name => "irsafe";
        public override string Help => "Escolhe a safe pra nascer (logo depois de entrar ou renascer).";
        public override string Syntax => "<numero>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length == 0) { P.AbrirEscolhaSafe(p, true); return; }
            var nome = P.AcharSafe(string.Join(" ", a));
            if (nome == null) { Chat.Erro(p, "Safe nao encontrada."); return; }
            P.EscolherSafe(p, nome);
        }
    }

    public class CmdItens : ComandoSistema
    {
        public override string Name => "itens";
        public override string Help => "Da pra voce as pecas de um modelo do /montar pra montar na mao (dono).";
        public override string Syntax => "<safe|negro|cacador> <1|2|3> | mais";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length > 0 && a[0].Equals("mais", StringComparison.OrdinalIgnoreCase)) { Chat.Info(p, P.EntregarPendentes(p)); return; }
            if (a.Length < 1) { Chat.Info(p, "Use /itens safe 1, /itens negro 1, /itens cacador 1 (e /itens mais pra pegar o resto)."); return; }
            var area = a[0].ToLowerInvariant();
            if (area == "caca" || area == "caçador") area = "cacador";
            if (area == "mercadonegro") area = "negro";
            int modelo = a.Length > 1 && int.TryParse(a[1], out var m) ? m : 1;
            P.ModelosRecarregar();
            Chat.Info(p, P.DarItensModelo(p, area, modelo));
        }
    }

    public class CmdPor : ComandoSistema
    {
        public override string Name => "por";
        public override string Help => "Coloca a peca que esta na sua mao onde voce olha, ate em lugar travado do mapa (dono).";
        public override string Syntax => "[giro em graus] | tirar | registrar [raio]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length > 0 && a[0].Equals("tirar", StringComparison.OrdinalIgnoreCase)) { Chat.Info(p, P.TirarNaMira(p)); return; }
            if (a.Length > 0 && a[0].Equals("registrar", StringComparison.OrdinalIgnoreCase))
            { Chat.Info(p, P.RegistrarPecas(p, a.Length > 1 && float.TryParse(a[1], out var rr) ? Mathf.Clamp(rr, 5f, 250f) : 80f)); return; }
            float giro = a.Length > 0 && float.TryParse(a[0], out var g) ? g : 0f;
            Chat.Info(p, P.PorNaMira(p, giro));
        }
    }

    /// <summary>/vaga add [carro|heli|aviao]: grava uma vaga onde você está (o carro tirado no terminal aparece nela).</summary>
    public class CmdVaga : ComandoSistema
    {
        public override string Name => "vaga";
        public override string Help => "Grava, lista ou apaga vagas de veiculo da garagem (dono).";
        public override string Syntax => "<add [carro|heli|aviao] | del | list>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            var cfg = P.Configuration.Instance;
            cfg.Vagas = cfg.Vagas ?? new List<Vaga>();
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            if (sub == "add")
            {
                var tipo = a.Length > 1 ? a[1].ToLowerInvariant() : "carro";
                if (tipo != "carro" && tipo != "heli" && tipo != "aviao") { Chat.Erro(p, "Tipo: carro, heli ou aviao."); return; }
                cfg.Vagas.Add(new Vaga { Tipo = tipo, X = p.Position.x, Y = p.Position.y, Z = p.Position.z, R = p.Player.look.yaw });
                P.Configuration.Save();
                int n = cfg.Vagas.Count(v => (new Vector3(v.X, v.Y, v.Z) - p.Position).sqrMagnitude < 80f * 80f);
                Chat.Ok(p, $"Vaga de {tipo} gravada aqui, virada pra onde voce olha ({n} vaga(s) nesta area). Coloque o TERMINAL da garagem perto (ate 80 m).");
                return;
            }
            if (sub == "del")
            {
                var perto = cfg.Vagas.OrderBy(v => (new Vector3(v.X, v.Y, v.Z) - p.Position).sqrMagnitude).FirstOrDefault();
                if (perto == null || (new Vector3(perto.X, perto.Y, perto.Z) - p.Position).sqrMagnitude > 25f) { Chat.Erro(p, "Nao tem vaga a menos de 5 m de voce."); return; }
                cfg.Vagas.Remove(perto); P.Configuration.Save();
                Chat.Ok(p, $"Vaga de {perto.Tipo} apagada.");
                return;
            }
            if (sub == "list")
            {
                var l = cfg.Vagas.Where(v => (new Vector3(v.X, v.Y, v.Z) - p.Position).sqrMagnitude < 200f * 200f).ToList();
                Chat.Info(p, l.Count == 0 ? "Nenhuma vaga nesta area." : $"{l.Count} vaga(s) aqui perto: " + string.Join(", ", l.Select(v => $"{v.Tipo} a {Mathf.RoundToInt(Vector3.Distance(p.Position, new Vector3(v.X, v.Y, v.Z)))} m")));
                return;
            }
            Chat.Info(p, "Use: /vaga add (carro), /vaga add heli, /vaga add aviao, /vaga del, /vaga list");
        }
    }
}
