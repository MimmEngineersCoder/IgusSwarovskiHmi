using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgusSwarovskiHmi.Messages
{
    internal class SelectProgramMessage : RequestMessage<bool>
    {

        public SelectProgramMessage(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; private set; }
    }
}
