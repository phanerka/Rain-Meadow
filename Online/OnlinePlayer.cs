﻿using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace RainMeadow
{
    public partial class OnlinePlayer : IEquatable<OnlinePlayer>
    {
        public MeadowPlayerId id;
        public ushort inLobbyId; // small id in lobby

        public Queue<OnlineEvent> OutgoingEvents = new(16);
        public List<OnlineEvent> recentlyAckedEvents = new(16);
        public List<OnlineEvent> abortedEvents = new();
        public Queue<OnlineState> OutgoingStates = new(128);

        private ushort nextOutgoingEvent = 1;
        public ushort lastEventFromRemote; // the last event I've received from them, I'll write it back on headers as an ack
        private ushort lastAckFromRemote; // the last event they've ack'd to me, used imediately on receive
        public ulong tick; // the last tick I've received from them, I'll write it back on headers as an ack
        public ulong lastAckdTick; // the last tick they've ack'd to me
        public bool needsAck;

        public bool isMe;
        public bool hasLeft;

        // DEBUG
        public bool eventsWritten;
        public bool statesWritten;
        public bool eventsRead;
        public bool statesRead;


        public OnlinePlayer(MeadowPlayerId id)
        {
            this.id = id;
        }

        public OnlineEvent QueueEvent(OnlineEvent e)
        {
            e.eventId = this.nextOutgoingEvent;
            e.to = this;
            e.from = LobbyManager.mePlayer;
            RainMeadow.Debug($"{e} for {this}");
            nextOutgoingEvent++;
            OutgoingEvents.Enqueue(e);
            return e;
        }

        public OnlineEvent GetRecentEvent(ulong id)
        {
            return recentlyAckedEvents.FirstOrDefault(e => e.eventId == id) ?? abortedEvents.FirstOrDefault(e => e.eventId == id);
        }

        public void EventAckFromRemote(ulong lastAck)
        {
            this.recentlyAckedEvents.Clear();
            this.lastAckFromRemote = lastAck;
            while (OutgoingEvents.Count > 0 && OnlineManager.IsNewerOrEqual(lastAck, OutgoingEvents.Peek().eventId))
            {
                var e = OutgoingEvents.Dequeue();
                RainMeadow.Debug($"{this} ackd {e}");
                recentlyAckedEvents.Add(e);
            }
        }

        public void TickAckFromRemote(ulong lastTick)
        {
            this.lastAckdTick = lastTick;
        }

        public bool HasUnacknoledgedEvents()
        {
            return OutgoingEvents.Count > 0;
        }

        public void AbortUnacknoledgedEvents()
        {
            if (OutgoingEvents.Count > 0)
            {
                RainMeadow.Debug($"Aborting events for player {this}");
                var toBeAborted = new Queue<OnlineEvent>(OutgoingEvents); // newly added events are not aborted on purpose
                OutgoingEvents.Clear();
                while (toBeAborted.Count > 0)
                {
                    var e = toBeAborted.Dequeue();
                    RainMeadow.Debug($"Aborting: {e}");
                    e.Abort();
                    abortedEvents.Add(e);
                }
            }
        }

        public override string ToString()
        {
            return $"{inLobbyId}:{id}";
        }

        // IEqu
        public override bool Equals(object obj) => this.Equals(obj as OnlinePlayer);
        public bool Equals(OnlinePlayer other)
        {
            return other != null && id == other.id;
        }
        public override int GetHashCode() => id.GetHashCode();

        public static bool operator ==(OnlinePlayer lhs, OnlinePlayer rhs)
        {
            return lhs is null ? rhs is null : lhs.Equals(rhs);
        }
        public static bool operator !=(OnlinePlayer lhs, OnlinePlayer rhs) => !(lhs == rhs);
    }
}
