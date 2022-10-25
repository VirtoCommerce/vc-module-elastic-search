namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchOptions
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Key { get; set; }
        public bool? EnableHttpCompression { get; set; } = false;
        public bool? EnableCompatibilityMode { get; set; } = false;
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
