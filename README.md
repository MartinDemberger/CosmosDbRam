# Cosmos DB RAM-Analysis

This project demonstrates the behaviour of CosmosDB-SDK to eat up a lot of RAM by creating Task-instances.

To produce the behaviour:

1. Start the local CosmosDB Emulator
2. Start the console app
3. Execute CreateEntries
4. Execute SelectEntries

The select only uses 100 parallel tasks each one selecting all items from database.

After all reading is completed there is a lot of memory consumed by objects I can't explain to myself:

* `Task<Object>` with 485 instances. I think the should be freed after reading is completed
* `Task+ContingentProperties` with 491 instances.
* `Pooledtimer` with 485 instances.
