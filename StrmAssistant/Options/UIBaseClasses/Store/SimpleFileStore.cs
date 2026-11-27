namespace StrmAssistant.Options.UIBaseClasses.Store
{
    using System;
    using System.IO;

    using Emby.Web.GenericEdit;
    using StrmAssistant.Common;

    using MediaBrowser.Common;
    using MediaBrowser.Common.Configuration;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;

    public class SimpleFileStore<TOptionType> : SimpleContentStore<TOptionType>
        where TOptionType : EditableOptionsBase, new()
    {
        private readonly ILogger logger;
        private readonly string pluginFullName;
        private readonly object lockObj = new object();
        private readonly IJsonSerializer jsonSerializer;
        private readonly IFileSystem fileSystem;
        private readonly string pluginconfigPath;

        private TOptionType options;
        private bool _loadFailed;

        public SimpleFileStore(IApplicationHost applicationHost, ILogger logger, string pluginFullName)
        {
            this.logger = logger;
            this.pluginFullName = pluginFullName;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();
            this.fileSystem = applicationHost.Resolve<IFileSystem>();

            var applicationPaths = applicationHost.Resolve<IApplicationPaths>();
            this.pluginconfigPath = applicationPaths.PluginConfigurationsPath;

            if (!this.fileSystem.DirectoryExists(this.pluginconfigPath))
            {
                this.fileSystem.CreateDirectory(this.pluginconfigPath);
            }

            this.OptionsFileName = string.Format("{0}.json", pluginFullName);
        }

        public event EventHandler<FileSavingEventArgs> FileSaving;

        public event EventHandler<FileSavedEventArgs> FileSaved;

        protected bool RuntimeActive => Plugin.IsPluginEnabled && !Plugin.IsShuttingDown;

        public virtual string OptionsFileName { get; }

        public string OptionsFilePath => Path.Combine(this.pluginconfigPath, this.OptionsFileName);

        public override TOptionType GetOptions()
        {
            lock (this.lockObj)
            {
                if (this.options == null)
                {
                    return this.ReloadOptions();
                }

                return this.options;
            }
        }

        public TOptionType ReloadOptions()
        {
            lock (this.lockObj)
            {
                try
                {
                    if (!this.fileSystem.FileExists(this.OptionsFilePath))
                    {
                        this.options ??= new TOptionType();
                        this._loadFailed = false;
                        return this.options;
                    }

                    using (var stream = this.fileSystem.OpenRead(this.OptionsFilePath))
                    {
                        var deserialized = new TOptionType().DeserializeFromJsonStream(stream, this.jsonSerializer);
                        this.options = deserialized as TOptionType ?? new TOptionType();
                        this._loadFailed = false;
                        return this.options;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("Error loading plugin options for {0} from {1}", ex, this.pluginFullName, this.OptionsFilePath);
                    this.options ??= new TOptionType();
                    this._loadFailed = true;
                    return this.options;
                }
            }
        }

        public override void SetOptions(TOptionType newOptions)
        {
            if (newOptions == null)
            {
                throw new ArgumentNullException(nameof(newOptions));
            }

            lock (this.lockObj)
            {
                if (this._loadFailed)
                {
                    throw new InvalidOperationException("Plugin configuration is corrupt. Repair or remove the file before saving.");
                }
            }

            var savingArgs = new FileSavingEventArgs(newOptions);
            this.FileSaving?.Invoke(this, savingArgs);

            if (savingArgs.Cancel)
            {
                return;
            }

            var tempPath = this.OptionsFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                lock (this.lockObj)
                {
                    using (var stream = this.fileSystem.GetFileStream(tempPath, FileOpenMode.Create, FileAccessMode.Write))
                    {
                        this.jsonSerializer.SerializeToStream(newOptions, stream, new JsonSerializerOptions { Indent = true });
                    }

                    AtomicFileReplacer.Replace(tempPath, this.OptionsFilePath);
                    this.options = newOptions;
                    this._loadFailed = false;
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            var savedArgs = new FileSavedEventArgs(newOptions);
            if (RuntimeActive)
            {
                FileSaved?.Invoke(this, savedArgs);
            }
        }
    }
}
