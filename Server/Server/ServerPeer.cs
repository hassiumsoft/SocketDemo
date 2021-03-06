﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
	/// <summary>
	/// 服务器端
	/// </summary>
	public class ServerPeer
	{
		private Socket serverSocket;            //客户端的socket对象
		private Semaphore acceptSemaphore;      //限制客户端连接数量的信号量
		private ClientPeerPool clientPeerPool;  //客户端对象的连接池
		private IApplication application;       //应用层

		public void SetApplication(IApplication _application)
		{
			application = _application;
		}


		public ServerPeer()
		{
		}
		/// <summary>
		/// 开启服务器
		/// </summary>
		/// <param name="_port"></param>
		/// <param name="_maxCount"></param>
		public void StartServer(int _port, int _maxCount)
		{
			try
			{
				serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				acceptSemaphore = new Semaphore(_maxCount, _maxCount);
				clientPeerPool = new ClientPeerPool(_maxCount);
				ClientPeer tClientPeer = null;


				for(int i = 0; i < _maxCount; i++)
				{
					tClientPeer = new ClientPeer();
					tClientPeer.ReceiveArgs.Completed += receiveComplete;
					tClientPeer.ReceiveCompletedDel += receiveCompleted;
					tClientPeer.SendDisconnect = Disconnect;
					clientPeerPool.Enqueue(tClientPeer);
				}
				serverSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
				serverSocket.Listen(_maxCount);
				Console.WriteLine("服务器启动！");
				startAccept(null);
			}
			catch(Exception _exception)
			{
				Console.WriteLine(_exception.Message);
			}
		}
		#region 接收客户端连接
		/// <summary>
		/// 开始等待客户端连接
		/// </summary>
		private void startAccept(SocketAsyncEventArgs _args)
		{
			if(null == _args)
			{
				_args = new SocketAsyncEventArgs();
				_args.Completed += acceptComplete;
			}
			bool tResult = serverSocket.AcceptAsync(_args);     //基于封装IO,效率更高；返回值判断异步事件是否执行完毕，如果返回true，代表正在执行，执行完毕后会触发
																
			if(tResult == false)                                //如果返回false，代表已经执行完成，直接处理
			{
				processAccept(_args);
			}
		}
		/// <summary>
		/// 接收连接异步请求事件完成时触发
		/// </summary>
		/// <param name="_sender"></param>
		/// <param name="_args"></param>
		private void acceptComplete(object _sender, SocketAsyncEventArgs _args)
		{
			processAccept(_args);
		}
		private void processAccept(SocketAsyncEventArgs _args)      //处理连接请求
		{
			//acceptSemaphore.WaitOne();  //计数，限制线程的访问
			//							//Socket tClientSocket = _args.AcceptSocket;      //得到客户端的对象
			//ClientPeer tClientPeer = clientPeerPool.Dequeue();  //从连接池中获取对象
			//tClientPeer.ClientSocket = _args.AcceptSocket;

			//Console.WriteLine("客户端连接成功 :" + tClientPeer.ClientSocket.RemoteEndPoint.ToString());

			//startReceive(tClientPeer);          //开始接受数据
			//_args.AcceptSocket = null;
			//startAccept(_args);


			//限制线程的访问
			acceptSemaphore.WaitOne();

			//得到客户端的对象 
			//Socket clientSocket = e.AcceptSocket;
			ClientPeer tClientPeer = clientPeerPool.Dequeue();
			tClientPeer.ClientSocket = _args.AcceptSocket;

			Console.WriteLine("客户端连接成功 :" + tClientPeer.ClientSocket.RemoteEndPoint.ToString());

			//开始接受数据
			startReceive(tClientPeer);

			_args.AcceptSocket = null;
			startAccept(_args);
		}
		#endregion


		#region 接收数据

		private void startReceive(ClientPeer _clientPeer)       //开始接收数据
		{
			try
			{
				bool result = _clientPeer.ClientSocket.ReceiveAsync(_clientPeer.ReceiveArgs);	//使用clientPeer中的Socket异步接收数据，放入clientPeer中的异步消息上下文对象中
				if(result == false)
				{
					processReceive(_clientPeer.ReceiveArgs);
				}
			}
			catch(Exception e)
			{
				Console.WriteLine(e.Message);
				throw;
			}
		}
		private void processReceive(SocketAsyncEventArgs _args)     //处理接收的请求
		{
			//Console.WriteLine("Enter");
			ClientPeer tClientPeer = _args.UserToken as ClientPeer;
			if(tClientPeer.ReceiveArgs.SocketError == SocketError.Success && tClientPeer.ReceiveArgs.BytesTransferred > 0)//判断网络消息是否接收成功
			{
				byte[] tPacket = new byte[tClientPeer.ReceiveArgs.BytesTransferred];
				Buffer.BlockCopy(tClientPeer.ReceiveArgs.Buffer, 0, tPacket, 0, tClientPeer.ReceiveArgs.BytesTransferred);          //拷贝到数组中
				tClientPeer.StartReceive(tPacket);                  //让客户端自身解析这个数据包
				startReceive(tClientPeer);                      //尾递归
			}
			else if(tClientPeer.ReceiveArgs.BytesTransferred == 0)          //如果传输字节数为0，表示连接断开
			{
				if(tClientPeer.ReceiveArgs.SocketError == SocketError.Success)       //客户端主动断开连接
				{
					Disconnect(tClientPeer, "客户端主动断开连接");
				}
				else       //由于网络异常导致被动断开连接
				{
					Disconnect(tClientPeer, tClientPeer.ReceiveArgs.SocketError.ToString());
				}
			}
		}
		private void receiveComplete(object _sender, SocketAsyncEventArgs _args)    //当接收完成时触发的事件
		{
			processReceive(_args);
		}
		/// <summary>
		/// 一条数据解析完成的处理
		/// </summary>
		/// <param name="_clientPeer">对应的连接对象</param>
		/// <param name="_value">解析出来的具体能使用的类型</param>
		private void receiveCompleted(ClientPeer _clientPeer, SocketMessage _socketMessage)
		{
			application.OnReceive(_clientPeer, _socketMessage);

		}
		#endregion

		#region 断开连接

		public void Disconnect(ClientPeer _clientPeer, string _disconnectReason)        //断开连接
		{
			try
			{
				if(null == _clientPeer)
				{
					throw new Exception("当前指定的客户端连接对象为空，无法断开连接");
				}
				application.OnDisConnect(_clientPeer);      //通知应用层，客户端断开连接了
				_clientPeer.Disconnect();
				clientPeerPool.Enqueue(_clientPeer);            //回收对象，方便下次使用
				acceptSemaphore.Release();
			}
			catch(Exception _exception)
			{
				Console.WriteLine(_exception.Message);
				throw;
			}
		}
		#endregion
	}
}






