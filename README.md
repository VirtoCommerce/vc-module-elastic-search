# Virto Commerce Elastic Search Module

[![CI status](https://github.com/VirtoCommerce/vc-module-elastic-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-elastic-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search)

> Note: The legacy ElasticModule is deprecated. All new implementations should use the [ElasticSearch8 module](https://github.com/VirtoCommerce/vc-module-elastic-search-8), which supports Elasticsearch versions 8 and 9.

The Virto Commerce Elastic Search module implements the ISearchProvider defined in the VirtoCommerce Search module. It leverages the Elasticsearch and OpenSearch engines to store indexed documents.

The module supports the following Elasticsearch deployment options:

* Standalone [Elasticsearch](https://www.elastic.co/products/elasticsearch)
* [Elastic Cloud](https://cloud.elastic.co/)
* [OpenSearch](https://opensearch.org/)
* [Amazon OpenSearch Service](https://aws.amazon.com/opensearch-service/)

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

> Virto Commerce has native [Elasticsearch 8.x module](https://github.com/VirtoCommerce/vc-module-elastic-search-8). The current module works with Elasticsearch 8.x in compatibility mode. 

For Elastic Cloud v8.x, use the following configuration and enable the compatibility mode:

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

### OpenSearch Service
For OpenSearch Service, use the following [Open Search Module](https://github.com/VirtoCommerce/vc-module-open-search):


## Documentation

* [Elastic Search module user documentation](https://docs.virtocommerce.org/platform/user-guide/elastic-search/overview/)
* [Elastic Search module developer documentation](https://docs.virtocommerce.org/platform/developer-guide/Fundamentals/Indexed-Search/integration/configuring-elasticsearch/)
* [REST API](https://virtostart-demo-admin.govirto.com/docs/index.html?urls.primaryName=VirtoCommerce.Search)
* [Elastic Search configuration](https://docs.virtocommerce.org/platform/developer-guide/Configuration-Reference/appsettingsjson/#elasticsearch)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-elastic-search)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-elastic-search/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
