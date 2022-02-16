# Blue Green indexation

Starting version 3.201.0 Elastic Search provider supports blue-green indexation. What that means is after running full index Rebuild + Delete once:
1. a new backup index will be created for Selected document type
2. indexation process will occur in this backup index
3. while full indexation is happening the current index will remain intact and all search operations will be done on it
4. after reindex is completed index swap will occur: the backup index will become active and the current will become backup
5. if you want to rollback to the old index use Swap index feature: click show backup indices, right click on a row for selected document type you want to switch and click swap index - the backup and active index will switch places
6. if you start Reindex process again the old backup will be lost and all indexation job will run on a new backup index.

## 1. Implementation details

Elastic Search provider implements blue-green indexation using Elastic Search aliases. Search provider implementations uses two aliases to diffirentiate between indices: active and backup. Full index alias constructed using scope name + document type name + alias name, so for example an active index alias for Members index using `default` scope will be `default-member-active`.
Each time you start Rebuild + Delete Elastic Search index provider looks for an existing backup index by backup alias (for example `default-member-backup`) and deletes it if it finds it. Then when the reidnexation start a new backup index is created it the `backup` alias. An actual index name is created dynamically however - a special alphanumerical token suffix is added to the end of the index name. The only way to tell which index is active is to look at their aliases. After the indexation process is finished a swap operation starts - active and backup indices switch aliases, so active becomes backup and vice versa.