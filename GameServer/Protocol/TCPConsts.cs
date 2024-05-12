﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Protocol
{
    //指令包的最大长度
    enum TCPCmdPacketSize { MAX_SIZE = 131072, RECV_MAX_SIZE = 6144 };

    /// <summary>
    /// 当前协议的版本号
    /// </summary>
    enum TCPCmdProtocolVer { VerSign = 20140624 };
}
