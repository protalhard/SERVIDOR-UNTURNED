using System;
using System.IO;
using System.Linq;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Log de baús e carros (pedido do dono, pra resolver denúncia de roubo): quem abriu baú de outra pessoa,
    /// quem tirou gasolina, trancou/destrancou ou entrou em carro de outra pessoa. Vai pro roubos_log.tsv e o site mostra.</summary>
    public partial class Plugin
    {
        string ArqRoubos => Path.Combine(Directory, "roubos_log.tsv");
        readonly object travaRoubos = new object();
        int linhasRoubos;

        void LigarLogRoubos()
        {
            BarricadeManager.onOpenStorageRequested += AoAbrirCaixaComboio;
            BarricadeManager.onOpenStorageRequested += AoAbrirBauLog;
            VehicleManager.onSiphonVehicleRequested += AoTirarGasolina;
            VehicleManager.onEnterVehicleRequested += AoEntrarCarro;
            VehicleManager.OnToggleVehicleLockRequested += AoTrancarCarro;
        }

        void DesligarLogRoubos()
        {
            BarricadeManager.onOpenStorageRequested -= AoAbrirCaixaComboio;
            BarricadeManager.onOpenStorageRequested -= AoAbrirBauLog;
            VehicleManager.onSiphonVehicleRequested -= AoTirarGasolina;
            VehicleManager.onEnterVehicleRequested -= AoEntrarCarro;
            VehicleManager.OnToggleVehicleLockRequested -= AoTrancarCarro;
        }

        /// <summary>tsv: quando(UTC) tipo quemId quemNome donoId donoNome oque X Z</summary>
        void LogRoubo(string tipo, ulong quem, ulong dono, string oque, Vector3 pos)
        {
            try
            {
                string L(string s) => Chat.Limpar(s ?? "").Replace('\t', ' ').Replace('\n', ' ');
                var fq = FaccaoDe(quem);
                if (dono != 0 && fq != null && fq == FaccaoDe(dono)) oque += " [mesma faccao]";
                var linha = $"{DateTime.UtcNow:o}\t{tipo}\t{quem}\t{L(NomeDe(quem))}\t{dono}\t{(dono == 0 ? "" : L(NomeDe(dono)))}\t{L(oque)}\t{pos.x:0}\t{pos.z:0}";
                lock (travaRoubos)
                {
                    File.AppendAllText(ArqRoubos, linha + Environment.NewLine);
                    if (++linhasRoubos >= 500)
                    {
                        linhasRoubos = 0;
                        var l = File.ReadAllLines(ArqRoubos);
                        if (l.Length > 20000) File.WriteAllLines(ArqRoubos, l.Skip(l.Length - 20000));
                    }
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        /// <summary>Mesma pessoa ou mesmo grupo/facção do jogo = não é de outra pessoa.</summary>
        static bool EhDoOutro(ulong quem, CSteamID dono, CSteamID grupoDono)
        {
            if (dono == CSteamID.Nil || dono.m_SteamID == quem) return false;
            var sp = PlayerTool.getSteamPlayer(quem);
            var meuGrupo = sp?.player?.quests?.groupID ?? CSteamID.Nil;
            return !(grupoDono != CSteamID.Nil && meuGrupo == grupoDono);
        }

        void AoAbrirBauLog(CSteamID quem, InteractableStorage bau, ref bool permitir)
        {
            if (!permitir || bau == null) return;
            try
            {
                if (!EhDoOutro(quem.m_SteamID, bau.owner, bau.group)) return;
                var drop = BarricadeManager.FindBarricadeByRootTransform(bau.transform);
                var nome = drop?.asset?.itemName ?? "Bau";
                LogRoubo("bau", quem.m_SteamID, bau.owner.m_SteamID, $"abriu {nome} ({bau.items?.getItemCount() ?? 0} itens dentro)", bau.transform.position);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void AoTirarGasolina(InteractableVehicle carro, Player quem, ref bool permitir, ref ushort quanto)
        {
            if (!permitir || carro == null || quem == null) return;
            try
            {
                var id = quem.channel.owner.playerID.steamID.m_SteamID;
                if (!EhDoOutro(id, carro.lockedOwner, carro.lockedGroup)) return;
                LogRoubo("gasolina", id, carro.lockedOwner.m_SteamID, $"tirou gasolina de {carro.asset?.vehicleName ?? "veiculo"} (tinha {carro.fuel})", carro.transform.position);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void AoEntrarCarro(Player quem, InteractableVehicle carro, ref bool permitir)
        {
            if (!permitir || carro == null || quem == null) return;
            try
            {
                var id = quem.channel.owner.playerID.steamID.m_SteamID;
                if (!EhDoOutro(id, carro.lockedOwner, carro.lockedGroup)) return;
                bool motorista = carro.passengers != null && carro.passengers.Length > 0 && carro.passengers[0].player == null;
                LogRoubo("carro", id, carro.lockedOwner.m_SteamID, $"entrou {(motorista ? "dirigindo " : "")}no {carro.asset?.vehicleName ?? "veiculo"}", carro.transform.position);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        void AoTrancarCarro(InteractableVehicle carro, ref bool permitir)
        {
            if (!permitir || carro == null) return;
            try
            {
                var quem = carro.passengers?[0].player;
                if (quem == null) return;
                var id = quem.playerID.steamID.m_SteamID;
                var nome = carro.asset?.vehicleName ?? "veiculo";
                if (carro.isLocked)
                    LogRoubo("carro", id, carro.lockedOwner.m_SteamID == id ? 0 : carro.lockedOwner.m_SteamID, "destrancou o " + nome, carro.transform.position);
                else if (EhDoOutro(id, carro.lockedOwner, carro.lockedGroup))
                    LogRoubo("carro", id, carro.lockedOwner.m_SteamID, "TRANCOU PRA SI o " + nome + " (o dono era outro)", carro.transform.position);
                else
                    LogRoubo("carro", id, 0, "trancou o " + nome, carro.transform.position);
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }
    }
}
