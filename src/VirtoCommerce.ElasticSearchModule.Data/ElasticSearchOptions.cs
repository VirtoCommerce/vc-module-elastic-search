namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchOptions
    {
        public string Server { get; set; }
        public string User { get; set; }
        /// <summary>
        /// Configures API Key.
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// Sets **true** value to enables gzip compressed requests and responses or **false** (default).
        /// </summary>
        public bool? EnableHttpCompression { get; set; } = false;
        /// <summary>
        /// Sets **true** value for using Elasticsearch v8.x or **false** (default) for earlier version. 
        /// </summary>
        public bool? EnableCompatibilityMode { get; set; } = false;
        /// <summary>
        /// 
        /// </summary>
        public string CertificateFingerprint { get; set; }

        /// <summary>
        /// Sets the default timeout in seconds for each request to Elasticsearch. Defaults to 60 seconds.
        /// </summary>
        public int RequestTimeout { get; set; } = 60;

        /// <summary>
        /// Sets the default timeout in seconds for health checking pings to Elasticsearch. Defaults to 2 seconds
        /// </summary>
        public int HealthCheckTimeout { get; set; } = 2;
    }
}
