﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Cosmos.Debug.VSDebugEngine {

  static public class DebugWindows {
    static public bool Enabled = true;
    static private NamedPipeClientStream mPipe;
    static private StreamWriter mWriter;

    static public void SendCommand(byte aCmd, byte[] aData) {
      if (!Enabled) {
        //return;
      }

      if (mPipe == null) {
        mPipe = new NamedPipeClientStream(".", @"Cosmos\DebugWindows", PipeDirection.Out);
        try {
          // For now we assume its there or not from the first call.
          // If we don't find the server, we disable it to avoid causing lag.
          // TODO: In future - try this instead:
          // String[] listOfPipes = System.IO.Directory.GetFiles(@"\.\pipe\");

          //mPipe.Connect(1000);
          mPipe.Connect();
        } catch (TimeoutException ex) {
          mPipe.Close();
          mPipe = null;
          Enabled = false;
          return;
        }
        mWriter = new StreamWriter(mPipe);
      }

      mPipe.WriteByte(aCmd);
      mPipe.WriteByte((byte)(aData.Length >> 8));
      mPipe.WriteByte((byte)(aData.Length & 0xFF));
      if (aData.Length > 0) {
        mPipe.Write(aData, 0, aData.Length);
      }
      mPipe.Flush();
    }

  }

}