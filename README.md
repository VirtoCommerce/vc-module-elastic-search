# VirtoCommerce.ElasticSearch

[![CI status](https://github.com/VirtoCommerce/vc-module-elastic-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-elastic-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-elastic-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-elastic-search)

VirtoCommerce.ElasticSearch module implements ISearchProvider defined in the VirtoCommerce.Core module and uses Elasticsearch engine which stores indexed documents on:
* Standalone <a href="https://www.elastic.co/products/elasticsearch" target="_blank">Elasticsearch</a> 
* <a href="https://cloud.elastic.co" target="_blank">Elastic Cloud</a> 
* <a href="https://aws.amazon.com/opensearch-service/" target="_blank">Amazon OpenSearch Service</a> (successor to Amazon Elasticsearch Service).

# Installation
Installing the module:
* Automatically: in VC Manager go to **Modules > Available**, select the **Elasticsearch module** and click **Install**.
* Manually: download module ZIP package from https://github.com/VirtoCommerce/vc-module-elastic-search/releases. In VC Manager go to **Modules > Advanced**, upload module package and click **Install**.

# Configuration
## VirtoCommerce.Search.SearchConnectionString
The search configuration string is a text string consisting of name/value pairs seaprated by semicolon (;). Name and value are separated by equal sign (=).

For Elasticsearch provider the configuration string must have three parameters:
```
provider=Elasticsearch;server=localhost:9200;scope=default
```

For Elastic Cloud, the configuration string must have four parameters:
```
provider=Elasticsearch;server=https://4fe3ad462de203c52b358ff2cc6fe9cc.europe-west1.gcp.cloud.es.io:9243;scope=default;key={SECRET_KEY}
```

* **provider** should be **Elasticsearch**
* **server** is a network address and port of the Elasticsearch server v5.x.
* **scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-documenttype`. One server can serve multiple indexes.
* **user** is a user name for either elastic cloud cluster or private elastic server. **Optional**. Default value is **elastic**.
* **key** is a password for either elastic cloud cluster or private elastic server. **Optional**.

You can configure the search configuration string either in the VC Manager UI or in VC Manager web.config. Web.config has higher priority.
* VC Manager: **Settings > Search > General > Search configuration string**
* web.config: **connectionStrings > SearchConnectionString**:
```
<connectionStrings>
    <add name="SearchConnectionString" connectionString="provider=Elasticsearch;server=localhost:9200;scope=default" />
</connectionStrings>
```

For Amazon OpenSearch Service, the configuration string must have these parameters:
```
provider=Elasticsearch;server=https://{master-user}:{master-user-password}@search-test-vc-c74km3tiav64fiimnisw3ghpd4.us-west-1.es.amazonaws.com;scope=default;
```

# License
Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
