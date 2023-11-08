using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IgusSwarovskiHmi.Models;
using OpenTK.Graphics.ES20;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgusSwarovskiHmi.ViewModels
{
    public partial class SelectProgramViewModel : ObservableObject
    {

        [ObservableProperty]
        private bool showSelectProgram = false;

        partial void OnShowSelectProgramChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                ProgramList = GetPrograms();
            }
        }

        [ObservableProperty]
        private List<IgusProgram> programList;

        public SelectProgramViewModel()
        {
            WeakReferenceMessenger.Default.Register<Messages.SelectProgramMessage>(this, (r, m) => ShowSelectProgram = false); ;
        }

        [RelayCommand]
        private void HideView()
        {
            ShowSelectProgram = false;
        }

        private List<IgusProgram> GetPrograms()
        {
            var progs = new List<IgusProgram>();
            foreach (var item in Directory.GetFiles(@"C:\iRC-igusRobotControl\Data\Programs"))
            {
                var prog = new IgusProgram();
                prog.FilePath = item;
                progs.Add(prog);
            }
            return progs;
        }

    }
}
