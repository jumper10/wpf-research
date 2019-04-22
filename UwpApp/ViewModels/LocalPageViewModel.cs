﻿using Data.Local.Data;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ViewModels;
using Windows.Storage.Pickers;

namespace UwpApp.ViewModels
{
    public class LocalPageViewModel: UWPViewModelBase
    {
        public ObservableCollection<Music> Musics { get; set; } = new ObservableCollection<Music>();

        private ICommand _selectDirectory;
        public ICommand SelectedDirectoryCommand
        {
            get
            {
                if(_selectDirectory == null)
                {
                    _selectDirectory = new RelayCommand(SelectDirectory);
                }
                return _selectDirectory;
            }
        }

        private ICommand _addFile;
        public ICommand AddFileCommand
        {
            get
            {
                if (_addFile == null)
                {
                    _addFile = new RelayCommand(AddFile);
                }
                return _addFile;
            }
        }

        private void SelectDirectory()
        {

        }

        public async void AddFile()
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add("*");
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var music = new Music { FilePath = file.Path, FileName = file.Name };
                Musics.Add(music);
                this.MessengerInstance.Send(music);
            }
        }
        public override void OnLoaded(object obj = null)
        {
            base.OnLoaded(obj);
        }
        public override void UnLoaded()
        {
            base.UnLoaded();
        }
    }
}
