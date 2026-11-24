using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    [RequireComponent(typeof(PhotonView))]
    public sealed class FPSChat : MonoBehaviourPunCallbacks
    {
        public const int CharacterLimit = 160;
        public const int HistoryLimit = 30;
        readonly Queue<string> messages = new Queue<string>();
        public int Revision { get; private set; }
        public string Text => string.Join("\n", messages);
        public string[] Messages => messages.ToArray();

        public bool Send(string text)
        {
            var room = FPSRoom.Instance;
            if (!PhotonNetwork.InRoom || room == null || room.MapLoading) return false;
            text = Clean(text);
            if (text.Length == 0) return false;
            photonView.RPC(nameof(ReceiveMessage), RpcTarget.All, text);
            return true;
        }

        // E17 uses an unbuffered room RPC. Names come from the real sender, not the message body.
        [PunRPC]
        void ReceiveMessage(string text, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.InRoom || info.Sender == null) return;
            text = Clean(text);
            if (text.Length == 0) return;
            var nickname = Clean(info.Sender.NickName);
            if (nickname.Length == 0) nickname = "unnamed";
            messages.Enqueue(nickname + ": " + text);
            while (messages.Count > HistoryLimit) messages.Dequeue();
            Revision++;
        }

        static string Clean(string text)
        {
            text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= CharacterLimit ? text : text.Substring(0, CharacterLimit);
        }

        public void Clear() { messages.Clear(); Revision++; }
        public override void OnLeftRoom() => Clear();
    }
}
