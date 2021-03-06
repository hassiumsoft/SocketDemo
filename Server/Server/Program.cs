﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
	class Program
	{
		/// <summary>
		/// 服务器：
		///     1.接收请求
		///     2.发送数据
		///     3.接收数据
		///     4.断开连接
		/// </summary>
		private static Socket serverSocket = null;
		static void Main(string[] args)
		{
			//AddressFamily.InterNetwork    地址族
			//SocketType.Stream     指定类型
			//ProtocolType.Tcp      指定协议
			serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//TCP以Stream(流)形式传输，而UDP以Dgram(数据报)形式传输
																									   //Socket tUDPSocket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
			IPEndPoint tEndPoint = new IPEndPoint(IPAddress.Any, 5056);       //服务器需要接受所有IP地址，端口号为0-65535
			serverSocket.Bind(tEndPoint);      //进程需要依赖于套接字，所以要将套接字绑在指定进程上
			serverSocket.Listen(10);           //连接队列的最大长度(最多等待的人数)=10


			//开启线程接收客户端连接(放在主线程中会一直等待导致卡死)
			Console.WriteLine("开始监听");
			Thread tThread = new Thread(listenClientConnect);
			tThread.Start();

			while(true)
			{
			}
		}

		private static void listenClientConnect()      //监听客户端连接
		{
			Socket tClientSocket = serverSocket.Accept();       //监听到客户端连接的时候，函数触发，返回一个客户端的Socket对象
			Console.WriteLine("监听到客户端连接" + tClientSocket.AddressFamily);

			tClientSocket.Send(Encoding.Default.GetBytes("Connect Success"));   //给客户端发送消息
			Thread tReceiveThread = new Thread(receiveClientMessage);
			tReceiveThread.Start(tClientSocket);

		}

		/// <summary>
		/// 接收来自客户端的消息 
		/// </summary>
		private static void receiveClientMessage(object _clientSocket)
		{
			Socket tClientSocket = _clientSocket as Socket;
			byte[] tBuffer = new byte[1024];
			int tLenght = tClientSocket.Receive(tBuffer);       //接收到数据的长度

			Console.WriteLine("收到消息：" + Encoding.Default.GetString(tBuffer, 0, tLenght));
            //Protocol tProtocol;

		}

        //private static void onResponse()
        //{
        //    switch(tProtocol)
        //    {
        //        case Protocol.Connect:
        //        Console.WriteLine("todo:玩家登录回应");
        //        break;
        //        case Protocol.CreateRoom:
        //        Console.WriteLine("todo:玩家创建房间回应");

        //        break;
        //        case Protocol.EnterRoom:
        //        Console.WriteLine("todo:玩家进入房间回应");

        //        break;
        //        case Protocol.Ready:
        //        Console.WriteLine("todo:玩家准备回应");

        //        break;
        //        case Protocol.StartGame:
        //        Console.WriteLine("todo:玩家开始游戏回应");

        //        break;
        //        case Protocol.DisbandRoom:
        //        Console.WriteLine("todo:玩家解散房间回应");

        //        break;
        //        case Protocol.CreateTeahouse:
        //        Console.WriteLine("todo:玩家创建茶馆回应");

        //        break;
        //        case Protocol.EnterTeahouse:
        //        Console.WriteLine("todo:玩家进入茶馆回应");

        //        break;
        //        case Protocol.CheckRecord:
        //        Console.WriteLine("todo:玩家查看记录回应");

        //        break;
        //        case Protocol.LogOff:
        //        Console.WriteLine("todo:玩家退出登录回应");

        //        break;
        //        default:
        //        break;
        //    }
        //}

	}
}
