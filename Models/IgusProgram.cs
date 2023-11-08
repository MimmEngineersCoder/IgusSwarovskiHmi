using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgusSwarovskiHmi.Models
{
    public partial class IgusProgram : ObservableObject
    {
        public string FilePath { get; set; }
        public string FileName { get => Path.GetFileName(FilePath); }
        public string ProgramName { get => Path.GetFileNameWithoutExtension(FilePath); }

        [RelayCommand]
        private void SelectProgram()
        {
            WeakReferenceMessenger.Default.Send(new Messages.SelectProgramMessage(FilePath));
        }


    }
}
