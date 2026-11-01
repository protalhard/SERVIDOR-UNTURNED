using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>DZ GUARD: anti-cheat do lado do servidor (o BattlEye continua ligado junto).
    /// Olha movimento (speed, teleporte, voo, atravessar parede) e tiro (distância, parede, cadência, mira).
    /// Caso claro = ban automático com foto da tela. Caso duvidoso = pontos na lista de suspeitos pra staff olhar.
    /// Arquivos: guard_log.txt (o bot manda pro #logs), suspeitos.tsv, reports.tsv, bans.tsv, banidos.tsv.</summary>
    public partial class Plugin
    {
        class FichaGuard
        {
            public Vector3 UltPos; public float UltTempo; public bool TemPos;
            public float LiberadoAte;
            public int SpeedSeguidos; public float SpeedPico;
            public float NoArDesde = -1, NoArY; public bool VooAvisado;
            public readonly List<float> Atravessou = new List<float>();
            public readonly List<float> TirosParede = new List<float>();
            public readonly List<(float t, bool cabeca, float dist)> Acertos = new List<(float, bool, float)>();
            public readonly Queue<float> Tiros = new Queue<float>(); public float FimAvisoCadencia, FimAvisoMira;
            public readonly LinkedList<string> Historico = new LinkedList<string>();
            public float ProxHist;
            public bool Banindo;
        }

        public class Suspeito
        {
            public ulong Id; public string Nome = ""; public int Pontos; public long Ultimo;
            public List<string> Motivos = new List<string>();
        }

        readonly Dictionary<ulong, FichaGuard> guard = new Dictionary<ulong, FichaGuard>();
        public readonly Dictionary<ulong, Suspeito> Suspeitos = new Dictionary<ulong, Suspeito>();
        readonly Dictionary<ulong, float> ultimoReport = new Dictionary<ulong, float>();
        readonly HashSet<ulong> alertados = new HashSet<ulong>();
        bool guardLigado; float proxExportar;

        static readonly int MascaraParede = RayMasks.BARRICADE | RayMasks.STRUCTURE;
        static readonly int MascaraChao = RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.STRUCTURE | RayMasks.BARRICADE | RayMasks.LARGE |
                                          RayMasks.MEDIUM | RayMasks.SMALL | RayMasks.ENVIRONMENT | RayMasks.VEHICLE | RayMasks.RESOURCE;
        string ArqGuardLog => Path.Combine(Directory, "guard_log.txt");
        string ArqSuspeitos => Path.Combine(Directory, "suspeitos.tsv");
        string ArqReports => Path.Combine(Directory, "reports.tsv");
        string ArqBans => Path.Combine(Directory, "bans.tsv");
        string ArqBanidos => Path.Combine(Directory, "banidos.tsv");
        string ArqComandos => Path.Combine(Directory, "admin_comandos.txt");

        static string Sem(string s) => (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        string NomeDe(ulong id) => PlayerTool.getSteamPlayer(id)?.playerID.characterName ?? (Stats.Existe(id) ? Stats.Obter(id).Nome : id.ToString());

        // ---------- Ligar / desligar ----------

        void LigarGuard()
        {
            if (guardLigado) return;
            guardLigado = true;
            CarregarSuspeitos();
            UseableGun.onBulletHit += AoAcertarTiro;
            UseableGun.onBulletSpawned += AoAtirar;
            InvokeRepeating(nameof(TickGuard), 2f, 0.5f);
        }

        void DesligarGuard()
        {
            if (!guardLigado) return;
            guardLigado = false;
            UseableGun.onBulletHit -= AoAcertarTiro;
            UseableGun.onBulletSpawned -= AoAtirar;
            CancelInvoke(nameof(TickGuard));
        }

        /// <summary>Ignora o movimento do jogador por uns segundos (teleporte do próprio servidor, renascer, sair do carro).</summary>
        public void GuardLiberar(ulong id, float segundos = 4f)
        {
            if (!guard.TryGetValue(id, out var g)) guard[id] = g = new FichaGuard();
            g.LiberadoAte = Math.Max(g.LiberadoAte, Time.realtimeSinceStartup + segundos);
            g.TemPos = false; g.NoArDesde = -1; g.SpeedSeguidos = 0;
        }

        bool Isento(SteamPlayer sp)
        {
            if (sp == null || sp.isAdmin) return true;
            var id = sp.playerID.steamID.m_SteamID;
            return EhDono(id) || staff.ContainsKey(id);
        }

        // ---------- Movimento (a cada 0,5 s) ----------

        void TickGuard()
        {
            try
            {
                if (S.GuardLigado) foreach (var sp in Provider.clients) ChecarMovimento(sp);
                if (Time.realtimeSinceStartup >= proxExportar) { proxExportar = Time.realtimeSinceStartup + 30f; ExportarBanidos(); }
                LerComandosAdmin();
                TickVotacao();
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void ChecarMovimento(SteamPlayer sp)
        {
            if (sp?.player == null) return;
            var id = sp.playerID.steamID.m_SteamID;
            if (!guard.TryGetValue(id, out var g)) guard[id] = g = new FichaGuard { LiberadoAte = Time.realtimeSinceStartup + 10f };
            var pl = sp.player;
            var agora = Time.realtimeSinceStartup;
            var pos = pl.transform.position;
            var carro = pl.movement.getVehicle();

            if (agora >= g.ProxHist)
            {
                g.ProxHist = agora + 2f;
                float vel = g.TemPos ? Vector3.Distance(Plano(pos), Plano(g.UltPos)) / Math.Max(0.1f, agora - g.UltTempo) : 0;
                Historico(g, $"pos {pos.x:0} {pos.y:0} {pos.z:0}  {vel:0.0} m/s" + (carro != null ? "  [veiculo]" : "") +
                    (pl.equipment.asset != null ? "  arma: " + pl.equipment.asset.itemName : ""));
            }

            bool pular = pl.life.isDead || carro != null || agora < g.LiberadoAte || Isento(sp) ||
                         pl.stance.stance == EPlayerStance.CLIMB || pl.stance.stance == EPlayerStance.SWIM ||
                         pl.stance.stance == EPlayerStance.DRIVING || pl.stance.stance == EPlayerStance.SITTING;
            if (pular)
            {
                if (carro != null || pl.life.isDead) g.LiberadoAte = Math.Max(g.LiberadoAte, agora + 3f);
                g.TemPos = false; g.NoArDesde = -1; g.SpeedSeguidos = 0;
                g.UltPos = pos; g.UltTempo = agora; g.TemPos = true;
                return;
            }
            if (!g.TemPos) { g.UltPos = pos; g.UltTempo = agora; g.TemPos = true; return; }

            float dt = Math.Max(0.1f, agora - g.UltTempo);
            float plano = Vector3.Distance(Plano(pos), Plano(g.UltPos));
            float v = plano / dt;
            var nome = sp.playerID.characterName;

            // 1) teleporte: pulo enorme de uma vez
            if (plano > S.GuardTeleporteMetros && !CarroPerto(pos))
            {
                Pontuar(id, nome, "TELEPORTE", 20, $"andou {plano:0} m em {dt:0.0}s ({g.UltPos.x:0} {g.UltPos.z:0} -> {pos.x:0} {pos.z:0})", false);
                g.SpeedSeguidos = 0;
            }
            // 2) speed hack: rápido demais por vários ticks seguidos
            else
            {
                float limite = S.GuardVelocidadeMax * Math.Max(1f, pl.movement.pluginSpeedMultiplier);
                if (v > limite && !CarroPerto(pos))
                {
                    g.SpeedSeguidos++; g.SpeedPico = Math.Max(g.SpeedPico, v);
                    if (g.SpeedSeguidos == 6)
                    {
                        bool grave = g.SpeedPico > limite * 2.2f;
                        Pontuar(id, nome, "SPEED HACK", grave ? 100 : 35, $"{g.SpeedPico:0.0} m/s por 3s (normal correndo: ate {limite:0} m/s)", grave);
                    }
                }
                else { g.SpeedSeguidos = 0; g.SpeedPico = 0; }
            }

            // 3) voo: no ar muito tempo, longe do chão e sem cair
            bool noChao = pl.movement.isGrounded;
            if (!noChao && pl.movement.totalGravityMultiplier > 0.5f)
            {
                if (g.NoArDesde < 0) { g.NoArDesde = agora; g.NoArY = pos.y; g.VooAvisado = false; }
                float tempoAr = agora - g.NoArDesde;
                if (tempoAr >= 6f && g.NoArY - pos.y < 3f)
                {
                    float altura = AlturaDoChao(pos);
                    if (altura > 5f && !g.VooAvisado)
                    {
                        g.VooAvisado = true;
                        Pontuar(id, nome, "VOO", 30, $"{tempoAr:0}s no ar a {altura:0} m do chao sem cair", false);
                    }
                    else if (altura > 20f && tempoAr >= 15f && g.NoArY - pos.y < 1f)
                    {
                        Pontuar(id, nome, "VOO", 100, $"{tempoAr:0}s parado no ar a {altura:0} m do chao", true);
                        g.NoArDesde = -1; g.LiberadoAte = agora + 60f; // não repete enquanto bane
                    }
                }
            }
            else g.NoArDesde = -1;

            // 4) atravessar parede: a linha entre a posição anterior e a nova corta parede/barricada nos 2 sentidos
            if (plano > 0.8f && plano < 6f && Mathf.Abs(pos.y - g.UltPos.y) < 1f)
            {
                var a = g.UltPos + Vector3.up * 1.2f; var b = pos + Vector3.up * 1.2f;
                if (Physics.Linecast(a, b, out var h1, MascaraParede, QueryTriggerInteraction.Ignore) &&
                    Physics.Linecast(b, a, out var h2, MascaraParede, QueryTriggerInteraction.Ignore) &&
                    !EhPorta(h1.transform) && !EhPorta(h2.transform) && Vector3.Distance(h1.point, h2.point) < 1.5f)
                {
                    g.Atravessou.Add(agora);
                    g.Atravessou.RemoveAll(t => agora - t > 120f);
                    if (g.Atravessou.Count >= 3)
                    {
                        Pontuar(id, nome, "ATRAVESSOU PAREDE", 35, $"passou por dentro de parede/barricada {g.Atravessou.Count}x em 2 min (perto de {pos.x:0} {pos.z:0})", false);
                        g.Atravessou.Clear();
                    }
                }
            }

            g.UltPos = pos; g.UltTempo = agora;
        }

        static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0, v.z);

        /// <summary>Em cima de carro/ônibus/trem andando: o jogador vai junto, não é speed.</summary>
        static bool CarroPerto(Vector3 pos)
        {
            foreach (var v in VehicleManager.vehicles)
            {
                if (v == null) continue;
                float d2 = (v.transform.position - pos).sqrMagnitude;
                if (d2 < 64f) return true;
                bool trem = v.trainCars != null && v.trainCars.Length > 0;
                if (d2 < (trem ? 250f * 250f : 30f * 30f) && Mathf.Abs(v.ReplicatedSpeed) > 2f) return true;
            }
            return false;
        }

        static float AlturaDoChao(Vector3 pos)
        {
            return Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out var h, 500f, MascaraChao, QueryTriggerInteraction.Ignore)
                ? h.distance : 500f;
        }

        static bool EhPorta(Transform t)
        {
            if (t == null) return false;
            var d = BarricadeManager.FindBarricadeByRootTransform(t.root);
            if (d?.interactable is InteractableDoor) return true;
            return t.GetComponentInParent<InteractableDoor>() != null;
        }

        // ---------- Tiro ----------

        void AoAtirar(UseableGun gun, BulletInfo bala)
        {
            try
            {
                if (!S.GuardLigado || gun?.player == null || bala == null || bala.pellet != 0) return;
                var sp = gun.player.channel.owner;
                if (Isento(sp)) return;
                var id = sp.playerID.steamID.m_SteamID;
                if (!guard.TryGetValue(id, out var g)) guard[id] = g = new FichaGuard();
                var asset = gun.equippedGunAsset;
                float agora = Time.realtimeSinceStartup;
                // firerate = ticks entre tiros (50 por segundo). Conta os tiros numa janela de 3 s
                // (o servidor recebe os tiros em lotes, então intervalo de um tiro pro outro não serve).
                float minimo = asset == null ? 0 : asset.firerate / (float)Math.Max(1u, PlayerInput.TOCK_PER_SECOND);
                g.Tiros.Enqueue(agora);
                while (g.Tiros.Count > 0 && agora - g.Tiros.Peek() > 3f) g.Tiros.Dequeue();
                if (minimo < 0.04f || agora < g.FimAvisoCadencia) return;
                int maximo = Mathf.CeilToInt(3f / minimo * 1.6f) + 4;
                if (g.Tiros.Count > maximo)
                {
                    g.FimAvisoCadencia = agora + 60f;
                    Pontuar(id, sp.playerID.characterName, "CADENCIA", 40,
                        $"{asset.itemName}: {g.Tiros.Count} tiros em 3s (o maximo dessa arma e uns {Mathf.CeilToInt(3f / minimo)})", false);
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void AoAcertarTiro(UseableGun gun, BulletInfo bala, InputInfo hit, ref bool permitir)
        {
            try
            {
                if (!S.GuardLigado || gun?.player == null || hit?.player == null || hit.player == gun.player) return;
                var sp = gun.player.channel.owner;
                var id = sp.playerID.steamID.m_SteamID;
                if (!guard.TryGetValue(id, out var g)) guard[id] = g = new FichaGuard();
                var olho = gun.player.look.aim.position;
                float dist = Vector3.Distance(olho, hit.point);
                var asset = gun.equippedGunAsset;
                var alvoNome = hit.player.channel.owner.playerID.characterName;
                bool cabeca = hit.limb == ELimb.SKULL;
                Historico(g, $"acertou {Chat.Limpar(alvoNome)} {(cabeca ? "na CABECA" : "no " + hit.limb.ToString().ToLowerInvariant())} a {dist:0} m ({asset?.itemName})");
                if (Isento(sp)) return;
                var nome = sp.playerID.characterName;
                float agora = Time.realtimeSinceStartup;

                // distância impossível: mais longe que o alcance da arma (com folga). Esse tiro não conta.
                float alcance = asset == null ? 0 : asset.range * Math.Max(1f, bala.magazineAsset != null ? bala.magazineAsset.range : 1f);
                if (alcance > 0 && dist > alcance * 1.5f + 15f)
                {
                    permitir = false;
                    Pontuar(id, nome, "DISTANCIA IMPOSSIVEL", 40, $"acertou {Chat.Limpar(alvoNome)} a {dist:0} m com {asset.itemName} (alcance {alcance:0} m)", false);
                    return;
                }

                // tiro através de parede/barricada (só parede de construção, perto)
                var dir = hit.point - olho;
                if (dist > 2f && dist < 150f &&
                    Physics.Raycast(olho, dir.normalized, out var h, dist - 0.6f, MascaraParede, QueryTriggerInteraction.Ignore) &&
                    !EhPorta(h.transform))
                {
                    g.TirosParede.Add(agora);
                    g.TirosParede.RemoveAll(t => agora - t > 600f);
                    if (g.TirosParede.Count >= 4)
                    {
                        Pontuar(id, nome, "TIRO ATRAVES DE PAREDE", 30, $"{g.TirosParede.Count} acertos atravessando parede em 10 min (ultimo: {Chat.Limpar(alvoNome)} a {dist:0} m)", false);
                        g.TirosParede.Clear();
                    }
                }

                // mira travada: muita cabeça de longe
                g.Acertos.Add((agora, cabeca, dist));
                g.Acertos.RemoveAll(x => agora - x.t > 1800f);
                var longe = g.Acertos.Where(x => x.dist > 40f).ToList();
                if (longe.Count >= 20 && agora > g.FimAvisoMira)
                {
                    double taxa = longe.Count(x => x.cabeca) / (double)longe.Count;
                    if (taxa >= 0.7)
                    {
                        g.FimAvisoMira = agora + 1800f;
                        Pontuar(id, nome, "MIRA TRAVADA", 40, $"{taxa:P0} dos acertos na cabeca a mais de 40 m ({longe.Count} acertos em 30 min)", false);
                    }
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        static void Historico(FichaGuard g, string linha)
        {
            g.Historico.AddLast($"{DateTime.Now:HH:mm:ss} {linha}");
            while (g.Historico.Count > 90) g.Historico.RemoveFirst();
        }

        string ResumoHistorico(ulong id, int linhas = 25)
            => guard.TryGetValue(id, out var g) ? string.Join(" | ", g.Historico.Skip(Math.Max(0, g.Historico.Count - linhas))) : "";

        // ---------- Pontos e punição ----------

        void Pontuar(ulong id, string nome, string tipo, int pontos, string detalhe, bool grave)
        {
            if (!Suspeitos.TryGetValue(id, out var s)) Suspeitos[id] = s = new Suspeito { Id = id };
            s.Nome = Sem(nome); s.Pontos += pontos; s.Ultimo = Estatisticas.Agora;
            s.Motivos.Add($"{DateTime.Now:dd/MM HH:mm} {tipo}: {Sem(detalhe)}");
            while (s.Motivos.Count > 15) s.Motivos.RemoveAt(0);
            SalvarSuspeitos();
            LogGuard($"{tipo} {Chat.Limpar(nome)} ({id}) +{pontos} (total {s.Pontos}): {detalhe}");

            if (grave && S.GuardBanAutomatico) { BanirGuard(id, tipo, detalhe); return; }
            if (s.Pontos >= S.GuardPontosAlerta && alertados.Add(id))
                AvisarStaff($"DZ GUARD: {Chat.Limpar(nome)} esta suspeito ({s.Pontos} pontos). Ultimo: {tipo}. Veja no /adm ou no site.");
        }

        void AvisarStaff(string msg)
        {
            foreach (var sp in Provider.clients)
            {
                if (sp?.player == null) continue;
                var p = UnturnedPlayer.FromSteamPlayer(sp);
                if (EhStaff(p)) Chat.Para(p, Chat.Cor(S.CorPerigo, msg));
            }
        }

        void LogGuard(string linha)
        {
            linha = $"{DateTime.Now:dd/MM HH:mm:ss} {linha}";
            Rocket.Core.Logging.Logger.Log("[GUARD] " + linha);
            try { File.AppendAllText(ArqGuardLog, linha + Environment.NewLine); } catch { }
        }

        /// <summary>Caso claro: tira foto da tela, espera uns segundos e bane (permanente ou o tempo da config).</summary>
        void BanirGuard(ulong id, string tipo, string detalhe)
        {
            var sp = PlayerTool.getSteamPlayer(id);
            if (sp?.player == null) return;
            if (!guard.TryGetValue(id, out var g)) guard[id] = g = new FichaGuard();
            if (g.Banindo) return;
            g.Banindo = true;
            var nome = sp.playerID.characterName;
            var evidencia = $"{tipo}: {detalhe} || {ResumoHistorico(id)}";
            string foto = TirarFoto(sp, "ban");
            uint dur = S.GuardBanHoras > 0 ? (uint)(S.GuardBanHoras * 3600) : SteamBlacklist.PERMANENT;
            StartCoroutine(DepoisDe(5f, () =>
            {
                var ip = sp.getIPv4AddressOrZero();
                var hw = sp.playerID.GetHwids();
                var motivo = "DZ GUARD: " + tipo;
                RegistrarBan(id, nome, 0, "DZ GUARD", motivo, dur, evidencia, foto);
                LogAdminNome("DZ GUARD", 0, $"BAN {nome} ({id}) {(dur == SteamBlacklist.PERMANENT ? "permanente" : S.GuardBanHoras + "h")}: {motivo}");
                Provider.requestBanPlayer(CSteamID.Nil, new CSteamID(id), ip, hw, motivo + ". Achou que foi erro? Abra ticket no Discord.", dur);
                Chat.Todos(Chat.Cor(S.CorPerigo, Chat.Forte("DZ GUARD: ")) + Chat.Cor(S.CorTexto, Chat.Limpar(nome) + " foi banido por " + tipo.ToLowerInvariant() + "."));
                ExportarBanidos();
            }));
        }

        System.Collections.IEnumerator DepoisDe(float s, System.Action f)
        {
            yield return new WaitForSeconds(s);
            try { f(); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Pede a foto da tela do jogador (ele não fica sabendo). Devolve o nome do arquivo que vai ser gravado em spy/.</summary>
        string TirarFoto(SteamPlayer sp, string tipo, CSteamID? mostrarPara = null)
        {
            var pasta = Path.Combine(Directory, "spy");
            System.IO.Directory.CreateDirectory(pasta);
            var id = sp.playerID.steamID.m_SteamID;
            var nome = sp.playerID.characterName;
            var arq = $"{id}_{DateTime.Now:yyyyMMdd_HHmmss}_{tipo}.jpg";
            sp.player.sendScreenshot(mostrarPara ?? CSteamID.Nil, (quem, jpg) =>
            {
                try
                {
                    File.WriteAllBytes(Path.Combine(pasta, arq), jpg);
                    LogGuard($"FOTO da tela de {Chat.Limpar(nome)} ({id}) [{tipo}] foto=spy/{arq}");
                }
                catch { }
            });
            return "spy/" + arq;
        }

        void LogAdminNome(string nome, ulong id, string acao)
        {
            var linha = $"{DateTime.Now:dd/MM HH:mm:ss} {Chat.Limpar(nome)} ({id}): {acao}";
            Rocket.Core.Logging.Logger.Log("[ADMIN] " + linha);
            try { File.AppendAllText(arqLogAdmin, linha + Environment.NewLine); } catch { }
        }

        // ---------- Registro de bans (com prova) ----------

        public void RegistrarBan(ulong id, string nome, ulong juiz, string juizNome, string motivo, uint dur, string evidencia, string foto)
        {
            try
            {
                File.AppendAllText(ArqBans, string.Join("\t", Estatisticas.Agora, id, Sem(nome), juiz, Sem(juizNome), dur,
                    Sem(motivo), Sem(evidencia), Sem(foto)) + Environment.NewLine);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        Dictionary<ulong, string[]> UltimoRegistroBan()
        {
            var r = new Dictionary<ulong, string[]>();
            try
            {
                if (File.Exists(ArqBans))
                    foreach (var l in File.ReadAllLines(ArqBans))
                    {
                        var c = l.Split('\t');
                        if (c.Length >= 9 && ulong.TryParse(c[1], out var id)) r[id] = c;
                    }
            }
            catch { }
            return r;
        }

        public class Banido
        {
            public ulong Id, Juiz; public string Nome, JuizNome, Motivo, Evidencia, Foto;
            public uint Banido_, Duracao, Resta; public bool Permanente;
        }

        public List<Banido> ListaBanidos()
        {
            var reg = UltimoRegistroBan();
            var l = new List<Banido>();
            foreach (var b in SteamBlacklist.list.ToList())
            {
                if (b == null || b.isExpired) continue;
                var id = b.playerID.m_SteamID;
                reg.TryGetValue(id, out var c);
                l.Add(new Banido
                {
                    Id = id, Juiz = b.judgeID.m_SteamID,
                    Nome = c != null ? c[2] : NomeDe(id),
                    JuizNome = c != null ? c[4] : (b.judgeID.m_SteamID == 0 ? "console" : NomeDe(b.judgeID.m_SteamID)),
                    Motivo = b.reason ?? "", Evidencia = c != null ? c[7] : "", Foto = c != null ? c[8] : "",
                    Banido_ = b.banned, Duracao = b.duration, Resta = b.getTime(),
                    Permanente = b.duration == SteamBlacklist.PERMANENT,
                });
            }
            return l.OrderByDescending(x => x.Banido_).ToList();
        }

        void ExportarBanidos()
        {
            try
            {
                var linhas = ListaBanidos().Select(b => string.Join("\t", b.Id, Sem(b.Nome), b.Juiz, Sem(b.JuizNome), Sem(b.Motivo),
                    b.Banido_, b.Permanente ? -1 : (long)b.Duracao, b.Permanente ? -1 : (long)b.Resta, Sem(b.Evidencia), Sem(b.Foto)));
                var tmp = ArqBanidos + ".tmp";
                File.WriteAllLines(tmp, linhas);
                if (File.Exists(ArqBanidos)) File.Delete(ArqBanidos);
                File.Move(tmp, ArqBanidos);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Desbane (painel do jogo, site ou /desbanir). Devolve false se ele não estava banido.</summary>
        public bool Desbanir(ulong quem, string quemNome, ulong id)
        {
            var nome = NomeDe(id);
            bool ok = SteamBlacklist.checkBanned(new CSteamID(id), out _);
            if (!ok) return false;
            Provider.requestUnbanPlayer(quem == 0 ? CSteamID.Nil : new CSteamID(quem), new CSteamID(id));
            if (SteamBlacklist.checkBanned(new CSteamID(id), out _)) { SteamBlacklist.unban(new CSteamID(id)); SteamBlacklist.save(); }
            if (Suspeitos.Remove(id)) SalvarSuspeitos();
            alertados.Remove(id);
            LogAdminNome(quemNome, quem, $"DESBAN {nome} ({id})");
            ExportarBanidos();
            return true;
        }

        /// <summary>Bane pelo Steam ID (online ou offline).</summary>
        public void BanirPorId(ulong quem, string quemNome, ulong id, double horas, string motivo)
        {
            var sp = PlayerTool.getSteamPlayer(id);
            var nome = NomeDe(id);
            if (string.IsNullOrWhiteSpace(motivo)) motivo = "Banido por um admin.";
            uint dur = horas > 0 ? (uint)Math.Min(uint.MaxValue - 1, horas * 3600) : SteamBlacklist.PERMANENT;
            var evid = Suspeitos.TryGetValue(id, out var s) ? string.Join(" | ", s.Motivos) : "";
            var hist = ResumoHistorico(id);
            if (hist != "") evid += (evid == "" ? "" : " || ") + hist;
            RegistrarBan(id, nome, quem, quemNome, motivo, dur, evid, "");
            LogAdminNome(quemNome, quem, $"BAN {nome} ({id}) {(dur == SteamBlacklist.PERMANENT ? "permanente" : horas + "h")}: {motivo}");
            if (sp != null) Provider.requestBanPlayer(new CSteamID(quem), new CSteamID(id), sp.getIPv4AddressOrZero(), sp.playerID.GetHwids(), motivo, dur);
            else Provider.requestBanPlayer(new CSteamID(quem), new CSteamID(id), 0, motivo, dur);
            ExportarBanidos();
        }

        // ---------- Suspeitos ----------

        void CarregarSuspeitos()
        {
            Suspeitos.Clear();
            try
            {
                if (!File.Exists(ArqSuspeitos)) return;
                foreach (var l in File.ReadAllLines(ArqSuspeitos))
                {
                    var c = l.Split('\t');
                    if (c.Length < 4 || !ulong.TryParse(c[0], out var id)) continue;
                    var s = new Suspeito { Id = id, Nome = c[1] };
                    int.TryParse(c[2], out s.Pontos); long.TryParse(c[3], out s.Ultimo);
                    if (c.Length > 4) s.Motivos = c[4].Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    Suspeitos[id] = s;
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void SalvarSuspeitos()
        {
            try
            {
                var tmp = ArqSuspeitos + ".tmp";
                File.WriteAllLines(tmp, Suspeitos.Values.OrderByDescending(s => s.Pontos)
                    .Select(s => string.Join("\t", s.Id, Sem(s.Nome), s.Pontos, s.Ultimo, string.Join(" | ", s.Motivos.Select(Sem)))));
                if (File.Exists(ArqSuspeitos)) File.Delete(ArqSuspeitos);
                File.Move(tmp, ArqSuspeitos);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        public bool IgnorarSuspeito(ulong quem, string quemNome, ulong id)
        {
            if (!Suspeitos.Remove(id)) return false;
            alertados.Remove(id);
            SalvarSuspeitos();
            LogAdminNome(quemNome, quem, $"LIMPOU suspeita de {NomeDe(id)} ({id})");
            return true;
        }

        // ---------- /report ----------

        public void Reportar(UnturnedPlayer quem, UnturnedPlayer alvo, string motivo)
        {
            var agora = Time.realtimeSinceStartup;
            ultimoReport[Id(quem)] = agora;
            var sp = alvo.Player.channel.owner;
            string foto = TirarFoto(sp, "report");
            var hist = ResumoHistorico(Id(alvo), 30);
            try
            {
                File.AppendAllText(ArqReports, string.Join("\t", Estatisticas.Agora, Id(alvo), Sem(alvo.CharacterName), Id(quem),
                    Sem(quem.CharacterName), Sem(motivo), foto, Sem(hist)) + Environment.NewLine);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            Pontuar(Id(alvo), alvo.CharacterName, "REPORT", 10, $"reportado por {Chat.Limpar(quem.CharacterName)}: {motivo}", false);
            AvisarStaff($"REPORT: {Chat.Limpar(quem.CharacterName)} reportou {Chat.Limpar(alvo.CharacterName)}: {Chat.Limpar(motivo)}");
        }

        public int ReportResta(UnturnedPlayer p)
            => ultimoReport.TryGetValue(Id(p), out var t) ? Math.Max(0, Mathf.CeilToInt(t + S.ReportIntervaloSegundos - Time.realtimeSinceStartup)) : 0;

        // ---------- Comandos do site (admin_comandos.txt) ----------
        // linha: acao \t steamDoAlvo \t steamDeQuemMandou \t horas \t motivo

        void LerComandosAdmin()
        {
            if (!File.Exists(ArqComandos)) return;
            var proc = ArqComandos + ".proc";
            string[] linhas;
            try
            {
                if (File.Exists(proc)) File.Delete(proc);
                File.Move(ArqComandos, proc);
                linhas = File.ReadAllLines(proc);
                File.Delete(proc);
            }
            catch { return; }
            foreach (var l in linhas)
            {
                var c = l.Split('\t');
                if (c.Length < 3 || !ulong.TryParse(c[1], out var alvo) || !ulong.TryParse(c[2], out var quem)) continue;
                // staff: só o dono (perm = campo 4, ligar = campo 3 "1")
                if (c[0] == "perm" || c[0] == "staff_remover")
                {
                    var donoNome = NomeDe(quem) + " (site)";
                    var r = c[0] == "perm" ? MudarPermissao(quem, donoNome, alvo, c.Length > 4 ? c[4] : "", c.Length > 3 && c[3] == "1")
                                           : RemoverStaffSite(quem, donoNome, alvo);
                    if (!EhDono(quem)) LogGuard($"COMANDO NEGADO do site: {c[0]} por {quem} (nao e dono)");
                    else Rocket.Core.Logging.Logger.Log("[ADMIN] site: " + r);
                    continue;
                }
                // wipe pelo painel (só o dono): salvar o mundo e apagar as bases dos jogadores, mantendo as safes
                if (c[0] == "salvar" || c[0] == "wipe_bases")
                {
                    if (!EhDono(quem)) { LogGuard($"COMANDO NEGADO do site: {c[0]} por {quem} (nao e dono)"); continue; }
                    string r;
                    try { r = c[0] == "salvar" ? SalvarTudo() : WipeBases(); }
                    catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); r = "erro: " + e.Message; }
                    LogAdminNome(NomeDe(quem) + " (site)", quem, c[0].ToUpperInvariant() + ": " + r);
                    try { File.WriteAllText(Path.Combine(Directory, c[0] + "_feito.txt"), r); } catch { }
                    continue;
                }
                // facção dissolvida pelo painel (só o dono): alvo = líder
                if (c[0] == "faccao_dissolver")
                {
                    if (!EhDono(quem)) { LogGuard($"COMANDO NEGADO do site: {c[0]} por {quem} (nao e dono)"); continue; }
                    LogAdminNome(NomeDe(quem) + " (site)", quem, "FACCAO: " + DissolverPelaStaff(alvo));
                    continue;
                }
                // limpar itens do chão pelo painel: precisa de "Mundo" ou ser dono (texto = segundos)
                if (c[0] == "limpar_itens")
                {
                    if (!EhDono(quem) && !(staff.TryGetValue(quem, out var ml) && ml.Perms.Contains("Mundo")))
                    { LogGuard($"COMANDO NEGADO do site: limpar_itens por {quem} (sem permissao Mundo)"); continue; }
                    int seg = c.Length > 4 && int.TryParse(c[4].Trim(), out var sv) ? Mathf.Clamp(sv, 0, 600) : 30;
                    if (seg == 0) LimparItens(); else AgendarLimpeza(seg);
                    LogAdminNome(NomeDe(quem) + " (site)", quem, seg == 0 ? "LIMPOU ITENS DO CHAO" : $"LIMPEZA DE ITENS em {seg}s");
                    continue;
                }
                // anúncio no chat do jogo (painel do site, aba Mensagens): precisa de "Mundo" ou ser dono
                if (c[0] == "anuncio")
                {
                    if (!EhDono(quem) && !(staff.TryGetValue(quem, out var ma) && ma.Perms.Contains("Mundo")))
                    { LogGuard($"COMANDO NEGADO do site: anuncio por {quem} (sem permissao Mundo)"); continue; }
                    var txt = Chat.Limpar(c.Length > 4 ? c[4] : "").Trim();
                    if (txt == "") continue;
                    for (int i = 0; i < 2; i++) Chat.Todos(Chat.Cor(S.CorDestaque, Chat.Forte("ANUNCIO: ")) + Chat.Cor("#FFFFFF", Chat.Forte(txt)));
                    LogAdminNome(NomeDe(quem) + " (site)", quem, "ANUNCIO: " + txt);
                    continue;
                }
                // confere de novo a permissão aqui (o site já conferiu)
                bool podeBan = EhDono(quem) || (staff.TryGetValue(quem, out var m) && m.Perms.Contains("Ban"));
                if (!podeBan) { LogGuard($"COMANDO NEGADO do site: {c[0]} por {quem} (sem permissao Ban)"); continue; }
                var quemNome = NomeDe(quem) + " (site)";
                switch (c[0])
                {
                    case "desbanir": Desbanir(quem, quemNome, alvo); break;
                    case "banir":
                        double.TryParse(c.Length > 3 ? c[3].Replace(',', '.') : "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h);
                        BanirPorId(quem, quemNome, alvo, h, c.Length > 4 ? c[4] : "");
                        break;
                    case "ignorar": IgnorarSuspeito(quem, quemNome, alvo); break;
                }
            }
        }
    }

    /// <summary>/report nome motivo: tira foto da tela do suspeito e manda pra staff.</summary>
    public class CmdReport : ComandoSistema
    {
        public override string Name => "report";
        public override string Help => "Denuncia um jogador suspeito de hack (manda foto da tela dele pra staff).";
        public override string Syntax => "<nome> <motivo>";
        public override List<string> Aliases => new List<string> { "denunciar", "reportar" };
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (a.Length < 2) { Chat.Erro(p, "Use: /report nome motivo   Ex: /report Joao atravessando parede"); return; }
            int resta = P.ReportResta(p);
            if (resta > 0) { Chat.Erro(p, $"Espere {resta}s pra reportar de novo."); return; }
            var alvo = UnturnedPlayer.FromName(a[0]);
            if (alvo == null) { Chat.Erro(p, "Jogador nao encontrado (ele precisa estar online)."); return; }
            if (alvo.CSteamID == p.CSteamID) { Chat.Erro(p, "Voce nao pode se reportar."); return; }
            var motivo = string.Join(" ", a.Skip(1));
            P.Reportar(p, alvo, motivo);
            Chat.Ok(p, $"Report de {Chat.Limpar(alvo.CharacterName)} enviado pra staff. Obrigado! Report falso pode dar punicao.");
        }
    }

    /// <summary>/banidos: lista os banidos (staff).</summary>
    public class CmdBanidos : ComandoSistema
    {
        public override string Name => "banidos";
        public override string Help => "Lista os jogadores banidos (staff).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Ban")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            var l = P.ListaBanidos();
            Chat.Para(p, Cor(S.CorPrincipal, Chat.Forte($"BANIDOS ({l.Count})")) + Cor(S.CorApagada, "  /desbanir steamid  |  lista completa no site /admin"));
            foreach (var b in l.Take(8))
                Chat.Para(p, Cor(S.CorTexto, $"{Chat.Limpar(b.Nome)} {b.Id}") + Cor(S.CorApagada, $"  {Chat.Limpar(b.Motivo)}  por {Chat.Limpar(b.JuizNome)}  " +
                    (b.Permanente ? "permanente" : "falta " + Chat.Tempo(b.Resta))));
        }
    }

    /// <summary>/desbanir steamid|nome (staff com permissão de Ban).</summary>
    public class CmdDesbanir : ComandoSistema
    {
        public override string Name => "desbanir";
        public override string Help => "Desbane um jogador (staff).";
        public override string Syntax => "<steamid ou nome>";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Ban")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            if (a.Length < 1) { Chat.Erro(p, "Use: /desbanir steamid (veja no /banidos)"); return; }
            var txt = string.Join(" ", a);
            ulong id;
            if (!ulong.TryParse(txt, out id))
            {
                var b = P.ListaBanidos().FirstOrDefault(x => x.Nome.IndexOf(txt, StringComparison.OrdinalIgnoreCase) >= 0);
                if (b == null) { Chat.Erro(p, "Nao achei ninguem banido com esse nome."); return; }
                id = b.Id;
            }
            if (P.Desbanir(Plugin.Id(p), p.CharacterName, id)) Chat.Ok(p, "Desbanido.");
            else Chat.Erro(p, "Esse jogador nao esta banido.");
        }
    }

    /// <summary>/espiao: invisível + imortal + câmera livre (voar) pra observar suspeitos.</summary>
    public class CmdEspiao : ComandoSistema
    {
        public override string Name => "espiao";
        public override string Help => "Modo espiao: invisivel, imortal e camera livre (staff).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.Pode(p, "Visao")) { Chat.Erro(p, "Voce nao tem permissao."); return; }
            bool ligar = !P.EmModoEspiao(p);
            P.ModoEspiao(p, ligar);
            if (ligar) Chat.Ok(p, "Modo espiao LIGADO: invisivel, imortal e camera livre liberada. /espiao de novo pra desligar.");
            else Chat.Info(p, "Modo espiao desligado.");
        }
    }
}
