﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GameServer.Cache;
using GameServer.Cache.Match;
using GameServer.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol.Code;
using Protocol.Code.Dto;
using Server;

namespace GameServer.Logic
{
	public class MatchHandler : IHandler
	{
		private MatchCache matchCache = Caches.Match;
		private PlayerCache playerCache = Caches.Player;
		public void OnDisconnect(ClientPeer _clientPeer)
		{
			string tUserId = playerCache.GetId(_clientPeer);
			if(matchCache.IsMatching(Convert.ToInt16(tUserId)))
			{
				leave(_clientPeer);
			}
		}
		public void OnReceive(ClientPeer _clientPeer, int _subCode, JObject _value)
		{
			switch(_subCode)
			{
				case MatchCode.EnterMatch_ClientReq:
				enter(_clientPeer);
				break;
				case MatchCode.LeaveMatch_ClientReq:
				leave(_clientPeer);
				break;
				case MatchCode.ReadyMatch_ClientReq:
				break;
			}
		}
		private void enter(ClientPeer _clientPeer)
		{
			SingleExecute.Instance.Excute(delegate ()
				{
					if(!playerCache.IsOnline(_clientPeer))
					{
						return;
					}
					int tUserId = Convert.ToInt16(playerCache.GetId(_clientPeer));
					if(matchCache.IsMatching(tUserId))      //检查用户是否已经在匹配
					{
						var tResponse = new
						{
							ErrorMsg = "用户已经在匹配",
						};
						string tRes = JsonConvert.SerializeObject(tResponse);
						_clientPeer.Send(OpCode.MATCH, MatchCode.EnterMatch_ServerRes, tRes);
						Console.WriteLine("用户已经在匹配");
						return;
					}
					MatchRoom tMatchRoom = matchCache.StartMatch(tUserId, _clientPeer);      //开始匹配
					tMatchRoom.Broadcast(OpCode.MATCH, MatchCode.EnterMatch_ServerBro, tUserId);//广播给房间内除当前玩家以外的所有玩家,有新玩家加入
					MatchRoomDto tMatchRoomDto = makeRoomDto(tMatchRoom);
					string tStr = JsonConvert.SerializeObject(tMatchRoomDto);
					_clientPeer.Send(OpCode.MATCH, MatchCode.EnterMatch_ServerRes, tStr);
				}
				);
		}       //进入房间
		private void leave(ClientPeer _clientPeer)          //离开房间
		{
			SingleExecute.Instance.Excute(delegate ()
			{
				int tUserID = Convert.ToInt16(playerCache.GetId(_clientPeer));
				if(!matchCache.IsMatching(tUserID))     //用户没有匹配，无法退出
				{
					var tErrorMsg = new
					{
						ErrorMsg = "用户没有匹配，无法退出",
					};
					string tStr = JsonConvert.SerializeObject(tErrorMsg);
					_clientPeer.Send(OpCode.MATCH, MatchCode.LeaveMatch_ServerBro, tStr);
					return;
				}
				MatchRoom tMatchRoom = matchCache.LeaveMatch(tUserID, _clientPeer);       //正常离开
																						  //广播给房间内所有人  有人离开了
				var tResMsg = new
				{
					UserId = tUserID,
				};
				string tRes = JsonConvert.SerializeObject(tResMsg);
				tMatchRoom.Broadcast(OpCode.MATCH, MatchCode.LeaveMatch_ServerBro, Convert.ToInt16(tUserID), _clientPeer);    //广播给房间内所有人，参数：离开的玩家id

			});
		}
		private MatchRoomDto makeRoomDto(MatchRoom _matchRoom)
		{
			//返回给当前客户端当前房间的数据模型
			MatchRoomDto tMatchRoomDto = new MatchRoomDto();
			foreach(var item in tMatchRoomDto.UidUserDic.Keys)
			{
				if(!playerCache.IsOnline(item.ToString()))
				{
					continue;
				}
				PlayerModel tPlayerModel = playerCache.GetPlayerModel(item.ToString());
				PlayerDto tPlayerDto = new PlayerDto(tPlayerModel.Nickname, tPlayerModel.Level, tPlayerModel.Exp);
				tMatchRoomDto.UidUserDic.Add(item, tPlayerDto);  //将当前玩家加入到匹配房间的Dic中
			}
			tMatchRoomDto.ReadyUidLst = _matchRoom.ReadyUIdLst;
			return tMatchRoomDto;
		}
	}
}
