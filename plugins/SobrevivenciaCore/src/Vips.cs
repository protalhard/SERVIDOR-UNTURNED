using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;

namespace SobrevivenciaCore
{
    /// <summary>VIP comprado pelo site (Pix do Mercado Pago). O site manda "vip" no admin_comandos.txt quando o
    /// pagamento é aprovado; aqui vira permissão com validade (vips.json), que o TemPermissao já confere.
    /// Comprar de novo o mesmo pacote soma os dias.</summary>
    public partial class Plugin
    {
        public class VipAtivo { public string Pacote, Escolha, Perms, Tag, Pagamento; public DateTime? Expira; public DateTime Comprou; }
        public class EstadoVips { public Dictionary<ulong, List<VipAtivo>> Jogadores = new Dictionary<ulong, List<VipAtivo>>(); public HashSet<string> Pagamentos = new HashSet<string>(); }

        EstadoVips vips;
        string ArqVips => Path.Combine(Directory, "vips.json");

        EstadoVips Vips()
        {
            if (vips != null) return vips;
            try { if (File.Exists(ArqVips)) vips = JsonConvert.DeserializeObject<EstadoVips>(File.ReadAllText(ArqVips)); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return vips = vips ?? new EstadoVips();
        }

        void SalvarVips()
        {
            try
            {
                var tmp = ArqVips + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Vips(), Formatting.Indented));
                if (File.Exists(ArqVips)) File.Delete(ArqVips);
                File.Move(tmp, ArqVips);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Pacote do site -> (permissões, dias (0 = pra sempre), tag no chat). escolha = kit escolhido nos pacotes "escolha 1".</summary>
        static bool PacoteVip(string pacote, string escolha, out string perms, out int dias, out string tag)
        {
            perms = null; dias = 30; tag = null;
            string[] iniciante = { "andarilho", "batedor", "saqueador" }, especial = { "zonaquente", "base", "raid" };
            switch (pacote)
            {
                case "iniciante": if (!iniciante.Contains(escolha)) return false; perms = "vip." + escolha; tag = escolha.ToUpperInvariant(); break;
                case "bandido": perms = "vip.bandido"; tag = "BANDIDO"; break;
                case "mercenario": perms = "vip.mercenario"; tag = "MERCENARIO"; break;
                case "comandante": perms = "vip.comandante"; tag = "COMANDANTE"; break;
                case "especial": if (!especial.Contains(escolha)) return false; perms = "vip." + escolha; tag = escolha == "zonaquente" ? "ZONA QUENTE" : escolha.ToUpperInvariant(); break;
                case "cofre-mochila": perms = "vault.vip"; tag = "COFRE"; break;
                case "cofre-bunker": perms = "vault.vip,vault.grande"; tag = "BUNKER"; break;
                case "cofre-fortaleza": perms = "vault.vip,vault.grande,vault.ultra"; tag = "FORTALEZA"; break;
                case "supremo": perms = "vip.supremo"; dias = 35; tag = "SUPREMO"; break;
                case "supremo-vitalicio": perms = "vip.supremo"; dias = 0; tag = "SUPREMO"; break;
                default: return false;
            }
            return true;
        }

        List<VipAtivo> VipsAtivos(ulong id)
        {
            if (!Vips().Jogadores.TryGetValue(id, out var l)) return new List<VipAtivo>();
            var agora = DateTime.UtcNow;
            return l.Where(v => v.Expira == null || v.Expira > agora).ToList();
        }

        bool VipTem(ulong id, string perm) => VipsAtivos(id).Any(v => (v.Perms ?? "").Split(',').Contains(perm));

        /// <summary>Tag do VIP mais forte pro chat ("" se não tem).</summary>
        string TagVip(ulong id)
        {
            var l = VipsAtivos(id);
            if (l.Count == 0) return "";
            var v = l.FirstOrDefault(x => x.Pacote.StartsWith("supremo")) ?? l.OrderByDescending(x => x.Comprou).First();
            return Chat.Cor(v.Pacote.StartsWith("supremo") ? "#FFD24A" : "#FFB020", "[" + v.Tag + "]") + " ";
        }

        string DarVip(ulong steam, string pacote, string escolha, string pagamento)
        {
            var st = Vips();
            if (!string.IsNullOrEmpty(pagamento) && st.Pagamentos.Contains(pagamento)) return "pagamento ja entregue: " + pagamento;
            if (!PacoteVip(pacote, escolha, out var perms, out var dias, out var tag)) return "pacote invalido: " + pacote + " " + escolha;
            if (!st.Jogadores.TryGetValue(steam, out var l)) st.Jogadores[steam] = l = new List<VipAtivo>();
            var agora = DateTime.UtcNow;
            var mesmo = l.FirstOrDefault(v => v.Pacote == pacote && v.Escolha == escolha && (v.Expira == null || v.Expira > agora));
            if (mesmo != null && mesmo.Expira != null)
            {
                mesmo.Expira = dias == 0 ? (DateTime?)null : mesmo.Expira.Value.AddDays(dias);
                mesmo.Pagamento = pagamento; mesmo.Comprou = agora;
            }
            else if (mesmo == null)
                l.Add(new VipAtivo { Pacote = pacote, Escolha = escolha, Perms = perms, Tag = tag, Pagamento = pagamento, Comprou = agora, Expira = dias == 0 ? (DateTime?)null : agora.AddDays(dias) });
            if (!string.IsNullOrEmpty(pagamento)) st.Pagamentos.Add(pagamento);
            SalvarVips();
            var nome = NomeJogador(steam);
            var sp = PlayerTool.getSteamPlayer(new CSteamID(steam));
            if (sp?.player != null)
            {
                var up = UnturnedPlayer.FromSteamPlayer(sp);
                Chat.Para(up, Chat.Cor(S.CorDestaque, Chat.Forte("VIP LIBERADO! ")) + Chat.Cor("#FFFFFF", $"Obrigado pela compra. Seu [{tag}] ja esta ativo" + (dias == 0 ? " pra sempre." : $" por {dias} dias.")));
                sp.player.ServerShowHint($"<color=#FFB020><b>VIP {tag} LIBERADO</b></color>\nObrigado por apoiar o servidor!", 6f);
            }
            Chat.Todos(Chat.Cor("#FFD24A", Chat.Forte("VIP: ")) + Chat.Cor("#FFFFFF", $"{Chat.Limpar(nome)} agora e [{tag}]. Valeu por apoiar o servidor!"));
            var r = $"VIP {pacote}{(string.IsNullOrEmpty(escolha) ? "" : " (" + escolha + ")")} pra {nome} ({steam}), pagamento {pagamento}";
            Rocket.Core.Logging.Logger.Log("[VIP] " + r);
            return r;
        }
    }
}
