using HOPEless.Bancho.Objects;

namespace Sunrise.Enums
{
    public enum PacketTypes : short
    {
        /// <summary>
        /// Sends the current status of the user to the server.
        /// <para>Data: <see cref="BanchoUserStatus"/></para>
        /// </summary>
        ClientUserStatus,

        /// <summary>
        /// Sends a chat message in a channel.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ClientChatMessagePublic,

        /// <summary>
        /// Close the Bancho connection. Parameter stands for quit reason: 0
        /// for normal exit and 1 for update.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientDisconnect,

        /// <summary>
        /// Ask the server for our own user data.
        /// <para>Expected response: <seealso cref="ServerUserData"/></para>
        /// </summary>
        ClientStatusRequestOwn,

        /// <summary>
        /// Response to Bancho's ping command.
        /// <para>Response to: <see cref="ServerPing"/></para>
        /// </summary>
        ClientPong,

        /// <summary>
        /// Reply to initial login. Contains an integer: positive means valid 
        /// login, negative is an error code.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerLoginReply,

        /// <summary>
        /// Originally meant for errors, now unused.
        /// </summary>
        ServerUnused1,

        /// <summary>
        /// Client receives a chat message. Can be in either a channel or PM.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ServerChatMessage,

        /// <summary>
        /// Ask the client for a pong response.
        /// <para>Expected response: <see cref="ClientPong"/></para>
        /// </summary>
        ServerPing,

        /// <summary>
        /// Informs client that somebody's username has changed. Data is in 
        /// format $"{oldUsername}>>>>{newUsername}".
        /// <para>Data: <seealso cref="BanchoString"/></para>
        /// </summary>
        ServerUserNameChanged,

        /// <summary>
        /// Originally an alternative to <seealso cref="ServerUserQuit"/>, now 
        /// unused.
        /// </summary>
        ServerUnused2,

        /// <summary>
        /// Tell the client data about a certain user, including Rank, 
        /// Accuracy, Score, <seealso cref="BanchoUserStatus"/>, and more.
        /// <para>Data: <see cref="BanchoUserData"/></para>
        /// <para>Response to: <seealso cref="ClientStatusRequestOwn"/></para>
        /// </summary>
        ServerUserData,

        /// <summary>
        /// Inform client that a user has disconnected.
        /// <para>Data: <see cref="BanchoUserQuit"/></para>
        /// </summary>
        ServerUserQuit,

        /// <summary>
        /// Inform the client that a spectator has joined. The parameter is the
        /// spectator's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerSpectateSpectatorJoined,

        /// <summary>
        /// Inform the client that a spectator has left.The parameter is the
        /// spectator's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerSpectateSpectatorLeft,

        /// <summary>
        /// Inform the client of what the spectated user is doing. This can
        /// include replay frames if the spectated user is playing.
        /// <para>Data: <see cref="BanchoReplayFrameBundle"/></para>
        /// </summary>
        ServerSpectateData,

        /// <summary>
        /// Inform the server that we started spectating. The parameter is the
        /// userID of the player we started spectating.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientSpectateStart,

        /// <summary>
        /// Inform the server that we stopped spectating. The parameter is the
        /// userID of the player we stopped spectating.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientSpectateStop,

        /// <summary>
        /// Inform the server of what we're doing. This can include replay 
        /// frames if we're playing.
        /// <para>Data: <see cref="BanchoReplayFrameBundle"/></para>
        /// </summary>
        ClientSpectateData,

        /// <summary>
        /// Tell the client that they should check for game client updates.
        /// </summary>
        ServerUpdateCheck,

        /// <summary>
        /// Originally used for error reports, now unused.
        /// </summary>
        ClientUnused3,

        /// <summary>
        /// Tell the server we don't have the beatmap that the spectated user
        /// is playing.
        /// </summary>
        ClientSpectateNoBeatmap,

        /// <summary>
        /// Inform the client that a spectator does not have your beatmap. The
        /// parameter is the spectator's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerSpectateNoBeatmap,

        /// <summary>
        /// Tell client to open the chat window.
        /// </summary>
        ServerChatFocus,

        /// <summary>
        /// Creates a notification bubble in the client. The parameter is the
        /// text to display.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerNotification,

        /// <summary>
        /// Sends a chat message to the user.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ClientChatMessagePrivate,

        /// <summary>
        /// Informs the client that the details of a multiplayer match have
        /// been updated.
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// </summary>
        ServerMultiMatchUpdate,

        /// <summary>
        /// Informs the client that a new multiplayer match has been created.
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// </summary>
        ServerMultiMatchNew,

        /// <summary>
        /// Informs the client that a multiplayer match has been deleted. The
        /// parameter is the ID of the deleted match.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerMultiMatchDelete,

        /// <summary>
        /// Inform bancho that we left the lobby, so stop sending us match
        /// updates already!
        /// </summary>
        ClientLobbyLeave,

        /// <summary>
        /// Informs bancho that we joined the lobby, so it can send us match
        /// information.
        /// </summary>
        ClientLobbyJoin,

        /// <summary>
        /// Tell bancho we created a multiplayer match.
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// </summary>
        ClientMultiMatchCreate,

        /// <summary>
        /// Tell bancho we want to join a match.
        /// <para>Data: <see cref="BanchoMultiplayerJoin"/></para>
        /// </summary>
        ClientMultiMatchJoin,

        /// <summary>
        /// Tell bancho we left the match.
        /// </summary>
        ClientMultiMatchLeave,

        /// <summary>
        /// Originally for joining the multiplayer lobby, now unused.
        /// </summary>
        ServerUnused4,

        /// <summary>
        /// Originally for leaving the multiplayer lobby, now unused.
        /// </summary>
        ServerUnused5,

        /// <summary>
        /// Tell the client they joined the match.
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// <para>Response to: <seealso cref="ClientMultiMatchCreate"/> or <seealso cref="ClientMultiMatchJoin"/></para>
        /// </summary>
        ServerMultiMatchJoinSuccess,

        /// <summary>
        /// Tell the client they failed to join the match.
        /// <para>Response to: <seealso cref="ClientMultiMatchCreate"/> or <seealso cref="ClientMultiMatchJoin"/></para>
        /// </summary>
        ServerMultiMatchJoinFail,

        /// <summary>
        /// Tell bancho we changed to a different slot. The parameter is the zero-based slot index.
        /// <para> Data: <see cref="BanchoInt"/> </para>
        /// </summary>
        ClientMultiSlotChange,

        /// <summary>
        /// Tell Bancho we're set to Ready.
        /// </summary>
        ClientMultiReady,

        /// <summary>
        /// Tell bancho we locked a slot. The parameter is the zero-based slot index.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiSlotLock,

        /// <summary>
        /// Tell bancho we changed the multiplayer settings
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// </summary>
        ClientMultiSettingsChange,

        /// <summary>
        /// Inform the client that another spectator joined. The parameter is
        /// the new spectator's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerSpectateOtherSpectatorJoined,

        /// <summary>
        /// Inform the client that another spectator left. The parameter is the 
        /// new spectator's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerSpectateOtherSpectatorLeft,

        /// <summary>
        /// Tell bancho that we started the game.
        /// </summary>
        ClientMultiMatchStart,

        /// <summary>
        /// Unused, unknown, likely intended as <seealso cref="ServerMultiAllPlayersLoaded"/>
        /// </summary>
        UnknwnUnused6,

        /// <summary>
        /// Tell the client that the multiplayer match has started.
        /// <para>Data: <see cref="BanchoMultiplayerMatch"/></para>
        /// </summary>
        ServerMultiMatchStart,

        /// <summary>
        /// Inform the client of the score of the other players.
        /// <para>Data: <see cref="BanchoScoreFrame"/></para>
        /// </summary>
        ClientMultiScoreUpdate,

        /// <summary>
        /// Inform Bancho of our score in the current multiplayer match.
        /// <para>Data: <see cref="BanchoScoreFrame"/></para>
        /// </summary>
        ServerMultiScoreUpdate,

        /// <summary>
        /// Tell bancho that we finished the multiplayer match.
        /// </summary>
        ServerMultiMatchCompleted,

        /// <summary>
        /// Tell the client they received host privileges in the multiplayer
        /// match.
        /// </summary>
        ServerMultiHostTransfer,

        /// <summary>
        /// Inform the server that we changed our current mods for this 
        /// multiplayer match.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiChangeMods,

        /// <summary>
        /// Tells the server that we have fully loaded the beatmap and are ready to play.
        /// </summary>
        ClientMultiMatchLoadComplete,

        /// <summary>
        /// Tells the client that all players have sent <seealso cref="ClientMultiMatchLoadComplete"/>
        /// </summary>
        ServerMultiAllPlayersLoaded,

        /// <summary>
        /// Tell bancho we don't have the selected beatmap for this multiplayer
        /// match.
        /// </summary>
        ClientMultiBeatmapMissing,

        /// <summary>
        /// Opposite of <seealso cref="ClientMultiReady"/>
        /// </summary>
        ClientMultiNotReady,

        /// <summary>
        /// Tell bancho we failed (reached 0 HP) in this multiplayer match.
        /// </summary>
        ClientMultiFailed,

        /// <summary>
        /// Tell the client that somebody failed. The parameter is the failed 
        /// player's user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerMultiOtherFailed,

        /// <summary>
        /// Tell the client that all players finished the map, and they should
        /// go to the results screen.
        /// </summary>
        ServerMultiMatchFinished,

        /// <summary>
        /// Tell bancho that we have the beatmap. Opposite of 
        /// <see cref="ClientMultiBeatmapMissing"/>
        /// </summary>
        ClientMultiBeatmapAvailable,

        /// <summary>
        /// Tell bancho we want to skip the intro of a song.
        /// </summary>
        ClientMultiSkipRequest,

        /// <summary>
        /// Tell the client that everybody skipped the intro of a song.
        /// </summary>
        ServerMultiSkip,

        /// <summary>
        /// Originally meant "unauthorized". Currently not in use.
        /// </summary>
        ServerUnused7,

        /// <summary>
        /// Tell bancho we want to join a chat channel. The parameter is the 
        /// name of the chat channel. If it is prefixed by a hash (#), it is a
        /// public channel. Otherwise, it is a PM channel.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ClientChatChannelJoin,

        /// <summary>
        /// Tell the client that they succesfully joined a channel. The 
        /// parameter is the name of the chat channel. If it is prefixed by a 
        /// hash character (#), it is a public channel. Otherwise, it is a PM 
        /// channel.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerChatChannelJoinSuccess,

        /// <summary>
        /// Tell the client that a chat channel is available. The parameter is 
        /// the name of the chat channel. If it is prefixed by a hash (#), it 
        /// is a public channel. Otherwise, it is a PM channel.
        /// <para>Data: <see cref="BanchoChatChannel"/></para>
        /// </summary>
        ServerChatChannelAvailable,

        /// <summary>
        /// Tell the client that they no longer have access to a channel. The 
        /// parameter is the name of the chat channel. If it is prefixed by a 
        /// hash character (#), it is a public channel. Otherwise, it is a PM 
        /// channel.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerChatChannelRevoked,

        /// <summary>
        /// Tell the client that a chat channel is available, and they should
        /// auto-join it. The parameter is the name of the chat channel. If it 
        /// is prefixed by a hash character (#), it is a public channel. 
        /// Otherwise, it is a PM channel.
        /// <para>Data: <see cref="BanchoChatChannel"/></para>
        /// </summary>
        ServerChatChannelAvailableAutojoin,

        /// <summary>
        /// Ask bancho for information on beatmaps.
        /// <para>Data: <see cref="BanchoBeatmapInfoRequest"/></para>
        /// <para>Expected response: <see cref="ServerBeatmapInfoReply"/></para>
        /// </summary>
        ClientBeatmapInfoRequest,

        /// <summary>
        /// Respond to a <seealso cref="ClientBeatmapInfoRequest"/> packet.
        /// <para>Data: <see cref="BanchoBeatmapInfoReply"/></para>
        /// </summary>
        ServerBeatmapInfoReply,

        /// <summary>
        /// Tell the server we want to transfer the host privileges to somebody
        /// else. The parameter is the user ID of the player to receive host 
        /// privileges.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiTransferHost,

        /// <summary>
        /// Tell client what permissions they have. Response to login packet. 
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerUserPermissions,

        /// <summary>
        /// Tell the server who their friends are. The parameter contains a 
        /// list of friend user ID's.
        /// <para>Data: <see cref="BanchoIntList"/></para>
        /// </summary>
        ServerFriendsList,

        /// <summary>
        /// Tell the server we just made a new friend! The parameter is the 
        /// user ID of our new friend.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientFriendsAdd,

        /// <summary>
        /// Tell the server we just lost a friend! The parameter is the user ID
        /// of that non-friend.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientFriendsRemove,

        /// <summary>
        /// Tell the client what Bancho version we are. The parameter is that 
        /// version.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerBanchoVersion,

        /// <summary>
        /// Tell the client to show a clickable image on the bottom of the main
        /// menu, which acts as a hyperlink. The parameter is a string in 
        /// format $"{imageUrl|linkUrl}".
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerMainMenuNews,

        /// <summary>
        /// Inform the server that we just toggled teams in the current 
        /// multiplayer match.
        /// </summary>
        ClientMultiChangeTeam,

        /// <summary>
        /// Tell the server we left a chat channel. The parameter is the name
        /// of that channel.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ClientChatChannelLeave,

        /// <summary>
        /// Ask the server for a list of players. This request is ignored by the server and nothing is sent in response.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientRequestPlayerList,

        /// <summary>
        /// Unused, originally used to trigger spyware (such as taking desktop screenshot, uploading files, uploading
        /// process list) but this logic has been removed.
        /// </summary>
        ServerUnused8,

        /// <summary>
        /// Another player has requested a skip. The parameter is said players
        /// user ID.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerMultiSkipRequestOther,

        /// <summary>
        /// Equivalent of /away &lt;message&gt; in IRC.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ClientAway,

        /// <summary>
        /// Gives the client user presence data.
        /// <para>Data: <see cref="BanchoUserPresence"/></para>
        /// </summary>
        ServerUserPresence,

        /// <summary>
        /// Unused, but seems to be used for IRC commands?
        /// </summary>
        UnknwnUnused9,

        /// <summary>
        /// Ask for <seealso cref="ServerUserData"/> for a list of users.
        /// <para>Data: <see cref="BanchoIntList"/></para>
        /// <para>Expected response: <see cref="ServerUserData"/></para>
        /// </summary>
        ClientUserStatsRequest,

        /// <summary>
        /// A bancho restart is scheduled. The parameter is how many minutes(?)
        /// are left before the restart.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerRestart,

        /// <summary>
        /// Tell server to send a multiplayer invite to a player. The parameter
        /// is the user ID of the recepient.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiInvite,

        /// <summary>
        /// Tell client they received a multiplayer invite.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ServerMultiInvite,

        /// <summary>
        /// Tell the client that we're done enumerating chat channels.
        /// </summary>
        ServerChatChannelListingComplete,

        /// <summary>
        /// Tell the server we want to change the password for the current 
        /// match.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ClientMultiChangePassword,

        /// <summary>
        /// Tell the client that the password for the multiplayer match has 
        /// been changed.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerMultiChangePassword,

        /// <summary>
        /// Tell the client that their client should be locked for x seconds,
        /// meaning they can not log in.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerLockClient,

        /// <summary>
        /// Request match information update. Seems to be related to tourney.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiMatchInfoRequest,

        /// <summary>
        /// Tells the client that somebody is muted.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerUserSilenced,

        /// <summary>
        /// Tells the client about a single user being online. Normally only sent in the login response.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ServerUserPresenceSingle,

        /// <summary>
        /// Tells the client about a single user being online. Collection version of
        /// <seealso cref="ServerUserPresenceSingle"/>.
        /// <para>Data: <see cref="BanchoIntList"/></para>
        /// </summary>
        ServerUserPresenceBundle,

        /// <summary>
        /// Request presence information from the server.
        /// <para>Data: <see cref="BanchoIntList"/></para>
        /// </summary>
        ClientUserPresenceRequest,

        /// <summary>
        /// Asks the server for presence information on all 
        /// <para>Data: <see cref="BanchoInt"/> (Current game time, ignored by server)</para>
        /// </summary>
        ClientUserPresenceRequestAll,

        /// <summary>
        /// Tell the server we wish to toggle blocking non-friend PMs.
        /// </summary>
        ClientUserToggleBlockNonFriendPm,

        /// <summary>
        /// Tell the client that the user they are trying to reach has blocked
        /// PMs from non-friends.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ServerChatPmBlocked,

        /// <summary>
        /// Tell the client that the user they are trying to reach is silenced.
        /// <para>Data: <see cref="BanchoChatMessage"/></para>
        /// </summary>
        ServerChatPmTargetSilenced,

        /// <summary>
        /// Tell the client that they should check for client updates, and
        /// restart right away after installing an update.
        /// </summary>
        ServerUpdateCheckForceRestart,

        /// <summary>
        /// Tell the client to use the backup bancho server c1.ppy.sh.
        /// </summary>
        ServerSwitchServer,

        /// <summary>
        /// Tells the client it is restricted, practically unused.
        /// </summary>
        ServerRestricted,

        /// <summary>
        /// Send the client a spooky message.
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        ServerRtx,

        /// <summary>
        /// Tell the server we quit the multi match.
        /// </summary>
        ClientMultiAbort,

        /// <summary>
        /// Tells the client they will be switched to a different server, for tournaments
        /// <para>Data: <see cref="BanchoString"/></para>
        /// </summary>
        BanchoSwitchTourneyServer,

        /// <summary>
        /// Tell the server we want to join some multiplayer channel.
        /// Tournament only.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiJoinChannel,

        /// <summary>
        /// Tell the server we left some multiplayer channel. Tournament only.
        /// <para>Data: <see cref="BanchoInt"/></para>
        /// </summary>
        ClientMultiLeaveChannel // This should be packet 109!
    }
}