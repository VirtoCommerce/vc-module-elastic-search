# Overview
The Virto Commerce Elastic Search module implements the ISearchProvider defined in the VirtoCommerce Search module. It leverages the Elasticsearch engine to store indexed documents.

The module supports the following Elasticsearch deployment options:

* Standalone [Elasticsearch](https://www.elastic.co/products/elasticsearch)
* [Elastic Cloud](https://cloud.elastic.co/)
* [Amazon OpenSearch Service](https://aws.amazon.com/opensearch-service/) (successor to Amazon Elasticsearch Service)

## Configuration
The Elastic Search provider can be configured using the following keys:

* **Search.Provider**: Specifies the search provider name, which must be set to "ElasticSearch".
* **Search.Scope**: Specifies the common name (prefix) for all indexes. Each document type is stored in a separate index, and the full index name is scope-{documenttype}. This allows one search service to serve multiple indexes. (Optional: Default value is "default".)
* **Search.ElasticSearch.Server**: Specifies the network address and port of the Elasticsearch server.
* **Search.ElasticSearch.User**: Specifies the username for either the Elastic Cloud cluster or private Elasticsearch server. (Optional: Default value is "elastic".)
* **Search.ElasticSearch.Key**: Specifies the password for either the Elastic Cloud cluster or private Elasticsearch server. (Optional)
* **Search.ElasticSearch.EnableCompatibilityMode**: Set this to "true" to use Elasticsearch v8.x or "false" (default) for earlier versions. (Optional)
* **Search.ElasticSearch.EnableHttpCompression**: Set this to "true" to enable gzip compressed requests and responses or "false" (default) to disable compression. (Optional)
* **Search.ElasticSearch.CertificateFingerprint**: During development, you can provide the server certificate fingerprint. When present, it is used to validate the certificate sent by the server. The fingerprint is expected to be the hex string representing the SHA256 public key fingerprint.

For more information about configuration settings, refer to the [Virto Commerce Configuration Settings documentation](https://virtocommerce.com/docs/user-guide/configuration-settings/).

## Samples
Here are some sample configurations for different scenarios:

### Elastic Cloud v8.x
For Elastic Cloud v8.x, use the following configuration:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "EnableCompatibilityMode": "true",
        "Server": "https://4fe3ad462de203c52b358ff2cc6fe9cc.europe-west1.gcp.cloud.es.io:9243",
        "User": "elastic",
        "Key": "{SECRET_KEY}"
    }
}
```

### Elasticsearch v8.x
For Elasticsearch v8.x without security features enabled:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "EnableCompatibilityMode": "true",
        "Server": "https://localhost:9200"
    }
}
```

For Elasticsearch v8.x with ApiKey authorization:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "EnableCompatibilityMode": "true",
        "Server": "https://localhost:9200",
        "User": "{USER_NAME}",
        "Key": "{SECRET_KEY}"
    }
}
```

### Elastic Cloud v7.x
For Elastic Cloud v7.x, use the following configuration:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "Server": "https://4fe3ad462de203c52b358ff2cc6fe9cc.europe-west1.gcp.cloud.es.io:9243",
        "User": "elastic",
        "Key": "{SECRET_KEY}"
    }
}
```

### Elasticsearch v7.x
For Elasticsearch v7.x without security features enabled:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "Server": "https://localhost:9200"
    }
}
```
For Elasticsearch v7.x with ApiKey authorization:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "Server": "https://localhost:9200",
        "User": "{USER_NAME}",
        "Key": "{SECRET_KEY}"
    }
}
```

### Amazon OpenSearch Service
For Amazon OpenSearch Service, use the following configuration:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "Server": "https://{master-user}:{master-user-password}@search-test-vc-c74km3tiav64fiimnisw3ghpd4.us-west-1.es.amazonaws.com"
    }
}
```


## Documentation
[Search Fundamentals](https://virtocommerce.com/docs/fundamentals/search/)

