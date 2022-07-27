# Virto Commerce Elastic Search Module

[![CI status](https://github.com/VirtoCommerce/vc-module-elastic-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-elastic-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search)

VirtoCommerce.ElasticSearch module implements ISearchProvider defined in the VirtoCommerce Search module and uses Elasticsearch engine which stores indexed documents on:
* Standalone <a href="https://www.elastic.co/products/elasticsearch" target="_blank">Elasticsearch</a> 
* <a href="https://cloud.elastic.co" target="_blank">Elastic Cloud</a> 
* <a href="https://aws.amazon.com/opensearch-service/" target="_blank">Amazon OpenSearch Service</a> (successor to Amazon Elasticsearch Service).

## Configuration
Azure Search provider are configurable by these configuration keys:

* **Search.Provider** is the name of the search provider and must be **ElasticSearch**
* **Search.ElasticSearch.Server** is a network address and port of the Elasticsearch server.
* **Search.ElasticSearch.User**  is a user name for either elastic cloud cluster or private elastic server. **Optional**. Default value is **elastic**.
* **Search.ElasticSearch.Key** is a password for either elastic cloud cluster or private elastic server. **Optional**.
* **Search.Scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-{documenttype}`. One search service can serve multiple indexes. **Optional**.  Default value is **default**.

[Read more about configuration here](https://virtocommerce.com/docs/user-guide/configuration-settings/)

For Elasticsearch provider v8.x the configuration string must have seven parameters:
Add additional fields **EnableCompatibilityMode** with **true** value for using Elasticsearch v8.x or **false** for earlier version and **CertificateFingerprint** for certificate fingerprint.
[Read more here](https://www.elastic.co/guide/en/elasticsearch/reference/8.1/configuring-stack-security.html)

```json
    "Search":{
        "Provider": "ElasticSearch",
        "Scope": "default",
        "ElasticSearch": {
            "Server": "https://localhost:9200",
            "User": "elastic",
            "Key": "{SECRET_KEY}",
            "EnableCompatibilityMode": "true",
            "CertificateFingerprint": "{CERTIFICATE_FINGERPRINT}"
         }
    }
```

For Elasticsearch provider the configuration string must have three parameters:
```json
    "Search":{
        "Provider": "ElasticSearch",
        "Scope": "default",
        "ElasticSearch": {
            "Server": "localhost:9200",
         }
    }
```

For Elastic Cloud, the configuration string must have four parameters:
```json
    "Search":{
        "Provider": "ElasticSearch",
        "Scope": "default",
        "ElasticSearch": {
            "Server": "https://4fe3ad462de203c52b358ff2cc6fe9cc.europe-west1.gcp.cloud.es.io:9243",
            "Key": "{SECRET_KEY}",
         }
    }
```

For Amazon OpenSearch Service, the configuration string must have these parameters:
```json
    "Search":{
        "Provider": "ElasticSearch",
        "Scope": "default",
        "ElasticSearch": {
            "Server": "https://{master-user}:{master-user-password}@search-test-vc-c74km3tiav64fiimnisw3ghpd4.us-west-1.es.amazonaws.com;",
         }
    }
```


## Documentation

* [Search Fundamentals](https://virtocommerce.com/docs/fundamentals/search/)

## References

* Deploy: https://virtocommerce.com/docs/latest/developer-guide/deploy-module-from-source-code/
* Installation: https://www.virtocommerce.com/docs/latest/user-guide/modules/
* Home: https://virtocommerce.com
* Community: https://www.virtocommerce.org
* [Download Latest Release](https://github.com/VirtoCommerce/vc-module-catalog/releases/latest)

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
