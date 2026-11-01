using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Vaga onde um veículo da garagem aparece (o /montar cria). Tipo: carro, heli ou aviao.</summary>
    public class Vaga
    {
        [XmlAttribute] public string Tipo = "carro";
        [XmlAttribute] public float X, Y, Z, R;
        public Vaga() { }
    }

    /// <summary>Garagem: veículo comprado vai pra garagem do jogador. No TERMINAL (barricada 51258) ele tira
    /// (aparece numa vaga livre do tipo certo: carro na vaga, heli no heliponto, avião na pista) ou guarda.
    /// Janela: efeito 51236 (NOMES_garagem_dz.md). Sem a janela, /garagem no chat.</summary>
    public partial class Plugin
    {
        public const ushort TerminalId = 51258;
        public const ushort GaragemId = 51236;
        public const short GaragemKey = 5127;
        const int GaragemPorPagina = 8;

        class Guardado { public ulong Dono; public ushort Id; public ushort Combustivel, Vida, Bateria; public bool Novo; }

        List<Guardado> garagem;
        class EstadoGaragem { public Vector3 Terminal; public int Pagina; public List<string> Tipos; }
        readonly Dictionary<ulong, EstadoGaragem> garagensAbertas = new Dictionary<ulong, EstadoGaragem>();
        string ArqGaragem => Path.Combine(Directory, "garagem.tsv");

        List<Guardado> Garagem
        {
            get
            {
                if (garagem != null) return garagem;
                garagem = new List<Guardado>();
                try
                {
                    if (File.Exists(ArqGaragem))
                        foreach (var l in File.ReadAllLines(ArqGaragem))
                        {
                            var c = l.Split('\t');
                            if (c.Length < 6 || !ulong.TryParse(c[0], out var d) || !ushort.TryParse(c[1], out var id)) continue;
                            ushort.TryParse(c[2], out var f); ushort.TryParse(c[3], out var v); ushort.TryParse(c[4], out var b);
                            garagem.Add(new Guardado { Dono = d, Id = id, Combustivel = f, Vida = v, Bateria = b, Novo = c[5] == "1" });
                        }
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                return garagem;
            }
        }

        void SalvarGaragem()
        {
            try { File.WriteAllLines(ArqGaragem, Garagem.Select(g => $"{g.Dono}\t{g.Id}\t{g.Combustivel}\t{g.Vida}\t{g.Bateria}\t{(g.Novo ? 1 : 0)}")); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        static string TipoVeiculo(VehicleAsset a)
            => a == null ? "carro" : a.engine == EEngine.HELICOPTER ? "heli" : a.engine == EEngine.PLANE ? "aviao" : "carro";

        static string NomeTipo(string t) => t == "heli" ? "Helicoptero" : t == "aviao" ? "Aviao" : "Carro";

        /// <summary>Tem vaga cadastrada? Se não, a loja ainda entrega o veículo direto (antes do /montar).</summary>
        public bool GaragemAtiva => Configuration.Instance.Vagas != null && Configuration.Instance.Vagas.Count > 0;

        public void GuardarComprado(UnturnedPlayer p, VehicleAsset a)
        {
            Garagem.Add(new Guardado { Dono = Id(p), Id = a.id, Novo = true });
            SalvarGaragem();
        }

        List<Guardado> DoJogador(ulong id, List<string> tipos = null)
            => Garagem.Where(g => g.Dono == id && (tipos == null || tipos.Contains(TipoVeiculo(Assets.find(EAssetType.VEHICLE, g.Id) as VehicleAsset)))).ToList();

        /// <summary>Vagas perto do terminal (mesma área).</summary>
        List<Vaga> VagasPerto(Vector3 terminal, float raio = 90f)
            => (Configuration.Instance.Vagas ?? new List<Vaga>()).Where(v => (new Vector3(v.X, v.Y, v.Z) - terminal).sqrMagnitude < raio * raio).ToList();

        static float RaioVaga(string tipo) => tipo == "aviao" ? 12f : tipo == "heli" ? 7f : 3f;

        Vaga VagaLivre(Vector3 terminal, string tipo)
        {
            var lista = new List<InteractableVehicle>();
            foreach (var v in VagasPerto(terminal).Where(v => v.Tipo == tipo).OrderBy(v => (new Vector3(v.X, v.Y, v.Z) - terminal).sqrMagnitude))
            {
                lista.Clear();
                VehicleManager.getVehiclesInRadius(new Vector3(v.X, v.Y, v.Z), RaioVaga(tipo), lista);
                if (lista.Count == 0) return v;
            }
            return null;
        }

        // ---------- ações ----------
        string Tirar(UnturnedPlayer p, Vector3 terminal, Guardado g)
        {
            var asset = Assets.find(EAssetType.VEHICLE, g.Id) as VehicleAsset;
            if (asset == null) return "Esse veiculo nao existe mais no servidor.";
            var tipo = TipoVeiculo(asset);
            if (!VagasPerto(terminal).Any(v => v.Tipo == tipo))
                return tipo == "heli" ? "Helicoptero sai no terminal do heliponto." : tipo == "aviao" ? "Aviao sai no terminal da pista." : "Carro sai no terminal do estacionamento.";
            var vaga = VagaLivre(terminal, tipo);
            if (vaga == null) return "Todas as vagas estao ocupadas. Espere liberar.";
            var v = VehicleManager.spawnLockedVehicleForPlayerV2(asset, new Vector3(vaga.X, vaga.Y + 1.5f, vaga.Z), Quaternion.Euler(0, vaga.R, 0), p.Player);
            if (v == null) return "Nao deu pra tirar o veiculo agora.";
            if (!g.Novo)
            {
                if (v.usesFuel) VehicleManager.sendVehicleFuel(v, g.Combustivel);
                if (v.usesHealth) VehicleManager.sendVehicleHealth(v, Math.Max((ushort)1, g.Vida));
                VehicleManager.sendVehicleBatteryCharge(v, g.Bateria);
            }
            Garagem.Remove(g);
            SalvarGaragem();
            LogAdmin(p, $"GARAGEM tirou {asset.vehicleName}");
            return $"{asset.vehicleName} esta na vaga, trancado pra voce.";
        }

        string Guardar(UnturnedPlayer p, Vector3 terminal)
        {
            var perto = new List<InteractableVehicle>();
            VehicleManager.getVehiclesInRadius(terminal, 60f, perto);
            var meu = p.Player.movement.getVehicle();
            var v = meu != null && meu.lockedOwner == p.CSteamID ? meu
                : perto.Where(x => x.lockedOwner == p.CSteamID && !x.isDead && !x.isExploded)
                       .OrderBy(x => (x.transform.position - p.Position).sqrMagnitude).FirstOrDefault();
            if (v == null) return "Traga o seu veiculo (trancado no seu nome) pra perto do terminal.";
            if ((v.transform.position - terminal).sqrMagnitude > 60f * 60f) return "Seu veiculo esta longe demais do terminal.";
            if (v.passengers.Any(s => s.player != null && s.player.playerID.steamID != p.CSteamID)) return "Tem gente dentro do veiculo.";
            if (v.trunkItems != null && v.trunkItems.getItemCount() > 0) return "Esvazie o porta-malas antes de guardar.";
            if (meu == v) VehicleManager.forceRemovePlayer(v, p.CSteamID);
            Garagem.Add(new Guardado { Dono = Id(p), Id = v.asset.id, Combustivel = v.fuel, Vida = v.health, Bateria = v.batteryCharge });
            SalvarGaragem();
            var nome = v.asset.vehicleName;
            VehicleManager.askVehicleDestroy(v);
            LogAdmin(p, $"GARAGEM guardou {nome}");
            return $"{nome} guardado na garagem.";
        }

        // ---------- janela ----------
        static void GT(UnturnedPlayer p, string n, string t) => EffectManager.sendUIEffectText(GaragemKey, Con(p), true, n, t ?? "");
        static void GV(UnturnedPlayer p, string n, bool on) => EffectManager.sendUIEffectVisibility(GaragemKey, Con(p), true, n, on);

        public void AbrirGaragem(UnturnedPlayer p, Vector3 terminal)
        {
            var tipos = VagasPerto(terminal).Select(v => v.Tipo).Distinct().ToList();
            if (tipos.Count == 0) tipos = new List<string> { "carro" };
            if (!(Assets.find(EAssetType.EFFECT, GaragemId) is EffectAsset asset))
            {
                ListarGaragemChat(p, tipos);
                return;
            }
            if (MenuAberto(p)) FecharMenu(p);
            FecharLoja(p); FecharAtm(p);
            garagensAbertas[Id(p)] = new EstadoGaragem { Terminal = terminal, Tipos = tipos };
            EffectManager.SendUIEffect(asset, GaragemKey, Con(p), true);
            p.Player.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
            GT(p, "Garagem_Titulo", "GARAGEM DZ  -  " + string.Join(" / ", tipos.Select(t => NomeTipo(t).ToUpperInvariant())));
            GT(p, "Garagem_Aviso", "");
            PreencherGaragem(p);
        }

        public void FecharGaragem(UnturnedPlayer p)
        {
            if (!garagensAbertas.Remove(Id(p))) return;
            EffectManager.askEffectClearByID(GaragemId, Con(p));
            if (!MenuAberto(p) && !lojasAbertas.ContainsKey(Id(p)) && !AtmAberto(p)) p.Player.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
        }

        void PreencherGaragem(UnturnedPlayer p)
        {
            if (!garagensAbertas.TryGetValue(Id(p), out var e)) return;
            var lista = DoJogador(Id(p), e.Tipos);
            int paginas = Math.Max(1, (lista.Count + GaragemPorPagina - 1) / GaragemPorPagina);
            e.Pagina = Mathf.Clamp(e.Pagina, 0, paginas - 1);
            for (int i = 0; i < GaragemPorPagina; i++)
            {
                int idx = e.Pagina * GaragemPorPagina + i;
                string n = "Garagem_Item_" + (i + 1);
                if (idx >= lista.Count) { GV(p, n, false); continue; }
                var g = lista[idx];
                var a = Assets.find(EAssetType.VEHICLE, g.Id) as VehicleAsset;
                GV(p, n, true);
                GT(p, n + "_Nome", (a?.vehicleName ?? ("Veiculo " + g.Id)).ToUpperInvariant());
                GT(p, n + "_Tipo", NomeTipo(TipoVeiculo(a)));
                GT(p, n + "_Estado", g.Novo ? "Novo" : $"Vida {(a != null && a.health > 0 ? 100 * g.Vida / a.health : 100)}%  |  Gasolina {(a != null && a.fuel > 0 ? 100 * g.Combustivel / a.fuel : 0)}%");
                if (!string.IsNullOrEmpty(S.IconeVeiculoUrl))
                    EffectManager.sendUIEffectImageURL(GaragemKey, Con(p), true, n + "_Icone", S.IconeVeiculoUrl.Replace("{id}", g.Id.ToString()), true, false);
            }
            GT(p, "Garagem_Pagina", $"{e.Pagina + 1}/{paginas}");
            if (lista.Count == 0) GT(p, "Garagem_Aviso", "Sua garagem esta vazia aqui. Compre veiculos na garagem (safe) ou no hangar (mercado negro).");
        }

        bool CliqueGaragem(UnturnedPlayer p, string botao)
        {
            if (!botao.StartsWith("Garagem_") || !garagensAbertas.TryGetValue(Id(p), out var e)) return false;
            switch (botao)
            {
                case "Garagem_Fechar": FecharGaragem(p); return true;
                case "Garagem_Anterior": e.Pagina--; PreencherGaragem(p); return true;
                case "Garagem_Proximo": e.Pagina++; PreencherGaragem(p); return true;
                case "Garagem_Guardar":
                {
                    var msg = Guardar(p, e.Terminal);
                    GT(p, "Garagem_Aviso", msg);
                    PreencherGaragem(p);
                    return true;
                }
            }
            if (botao.StartsWith("Garagem_Item_") && botao.EndsWith("_Tirar") && int.TryParse(botao.Substring(13, botao.Length - 19), out var n))
            {
                var lista = DoJogador(Id(p), e.Tipos);
                int idx = e.Pagina * GaragemPorPagina + n - 1;
                if (idx >= 0 && idx < lista.Count)
                {
                    var msg = Tirar(p, e.Terminal, lista[idx]);
                    GT(p, "Garagem_Aviso", msg);
                    PreencherGaragem(p);
                }
            }
            return true;
        }

        // ---------- chat (sem a janela) ----------
        void ListarGaragemChat(UnturnedPlayer p, List<string> tipos)
        {
            var lista = DoJogador(Id(p), tipos);
            if (lista.Count == 0) { Chat.Info(p, "Sua garagem esta vazia aqui. /garagem guardar guarda o veiculo que esta com voce."); return; }
            Chat.Info(p, "Sua garagem (use /garagem tirar numero):");
            for (int i = 0; i < lista.Count && i < 10; i++)
                Chat.Para(p, Chat.Cor(S.CorPrincipal, $"{i + 1}. ") + Chat.Cor(S.CorTexto, (Assets.find(EAssetType.VEHICLE, lista[i].Id) as VehicleAsset)?.vehicleName ?? ("Veiculo " + lista[i].Id)));
        }

        public Vector3? TerminalPerto(Vector3 pos, float raio = 8f)
        {
            foreach (var reg in BarricadeManager.regions)
                foreach (var d in reg.drops)
                    if (d.asset != null && d.asset.id == TerminalId && (d.model.position - pos).sqrMagnitude < raio * raio) return d.model.position;
            return null;
        }

        public void ComandoGaragem(UnturnedPlayer p, string[] a)
        {
            var t = TerminalPerto(p.Position);
            if (t == null) { Chat.Erro(p, "Va ate um TERMINAL da garagem (estacionamento, heliponto ou pista)."); return; }
            var tipos = VagasPerto(t.Value).Select(v => v.Tipo).Distinct().ToList();
            if (a.Length >= 1 && a[0] == "guardar") { Chat.Info(p, Guardar(p, t.Value)); return; }
            if (a.Length >= 2 && a[0] == "tirar" && int.TryParse(a[1], out var n))
            {
                var lista = DoJogador(Id(p), tipos);
                if (n < 1 || n > lista.Count) { Chat.Erro(p, "Numero invalido."); return; }
                Chat.Info(p, Tirar(p, t.Value, lista[n - 1]));
                return;
            }
            AbrirGaragem(p, t.Value);
        }
    }

    public class CmdGaragem : ComandoSistema
    {
        public override string Name => "garagem";
        public override string Help => "Garagem dos seus veiculos (perto de um terminal).";
        public override string Syntax => "[tirar numero | guardar]";
        protected override void Rodar(UnturnedPlayer p, string[] a) => P.ComandoGaragem(p, a);
    }
}
