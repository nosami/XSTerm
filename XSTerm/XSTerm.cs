﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Foundation;
using Mono.Addins;
using Mono.Unix.Native;
using MonoDevelop.Components;
using MonoDevelop.Core;
using WebKit;

namespace XSTerm
{
	public class XSTerm : MonoDevelop.Ide.Gui.PadContent
	{
		readonly Terminal terminal;
		public XSTerm()
		{
			terminal = new Terminal();
		}

		public override Control Control => terminal;
	}

	class Terminal : Control
	{
		readonly WebView webView;

		protected override object CreateNativeWidget<T>()
		{
			return webView;
		}

		public Terminal()
		{
			webView = new WebView();
			webView.SetValueForKey(NSObject.FromObject(false), new NSString("drawsBackground"));

			var rootPath = AddinManager.CurrentAddin.GetFilePath();
			var pathToNode = Path.Combine(rootPath, "node");
			if (!File.Exists(pathToNode))
			{
				// dev
				rootPath = AddinManager.CurrentAddin.GetFilePath("..", "..");
				pathToNode = Path.Combine(rootPath, "node");
			}
			// The MonoDevelop addin downloader loses file permissions. We need execute permission.
			Syscall.chmod(pathToNode, FilePermissions.S_IRWXU | FilePermissions.S_IRGRP | FilePermissions.S_IROTH);

			var pathToApp = Path.Combine(rootPath, "xterm.js", "demo", "app.js");
			var port = FreeTcpPort();
			var psi = new ProcessStartInfo
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				FileName = pathToNode,
				RedirectStandardOutput = true,
				Arguments = $"\"{pathToApp}\" {port}"
			};
			var process = Process.Start(psi);
			process.BeginOutputReadLine();
			process.EnableRaisingEvents = true;
			process.OutputDataReceived += (s, e) =>
			{
				if (!(e.Data == null))
				{
					LoggingService.LogInfo("Terminal - " + e.Data);
					if (e.Data.StartsWith("App listening", StringComparison.Ordinal))
						Runtime.RunInMainThread(() => webView.MainFrameUrl = $"http://127.0.0.1:{port}");
				}
			};
		}

		static int FreeTcpPort()
		{
			var l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			return port;
		}
	}
}
