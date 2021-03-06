﻿using System.Reactive.Disposables;

namespace TextFileLoader
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Text;
    using Cinch;
    using DynamicData;
    using ReactiveUI;

    public class MainViewModel : ReactiveObject, IDisposable
    {
        private readonly IDisposable cleanUp;
        private readonly IOpenFileService openFileService;
        private readonly ISubject<string> files = new Subject<string>();
        private readonly ReadOnlyObservableCollection<string> linesCollection;
        
        public MainViewModel(IOpenFileService openFileService)
        {
            this.openFileService = openFileService;
            OpenFileCommand = ReactiveCommand.Create();
            OpenFileCommand.Subscribe(_ => OpenFromFile());


            var list = new SourceList<string>();
            var listLoader = list.Connect()
                .ObserveOnDispatcher()
                .Bind(out linesCollection)
                .Subscribe();

            var linesWriter = files
                .Select(path =>
                {
                    return Observable.Using(() => new StreamReader(path, Encoding.Default), CreateObservableLines)
                        .Finally(() => list.Clear());
                })
                .Switch()
                .Subscribe(line => list.Add(line));

            cleanUp = new CompositeDisposable(listLoader, linesWriter, list);
        }

        public IEnumerable<string> LinesCollection => linesCollection;

        private void OpenFromFile()
        {
            var dialogResult = openFileService.ShowDialog(null);
            if (dialogResult == true)
            {
                var path = openFileService.FileName;
                files.OnNext(path);
            }
        }

        private static IObservable<string> CreateObservableLines(StreamReader reader)
        {
            var linesFromFile = ToLines(reader);

            var observableLinesFromFile = linesFromFile
                .ToObservable()
                .PushEvery(TimeSpan.FromSeconds(2));

            return observableLinesFromFile;
        }

        private static IEnumerable<string> ToLines(StreamReader streamReader)
        {
            while (!streamReader.EndOfStream)
            {
                yield return streamReader.ReadLine();
            }
        }

        public ReactiveCommand<object> OpenFileCommand { get; }
        public void Dispose()
        {
            cleanUp.Dispose();
        }
    }
}