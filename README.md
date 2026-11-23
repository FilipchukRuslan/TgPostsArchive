`How to Run`

Clone the repository:
git clone https://github.com/FilipchukRuslan/TgPostsArchive.git

cd TgPostsArchive

git checkout master

Create .env file in the project root:
POSTGRES_PASSWORD=your_password

Build and start the application:
docker compose up --build -d

Open the application at http://localhost:8080.



`Short description of implementation`:
First I found some existing patterns to work with telegram html.
Created the client and the parser and checked if its working.
Then I continued with db schema and created the entities.
Next step was to create background worker and collector service.
It took me about 7-8 hours.
Next step was to create frontend. After ckecking the overall functionality,
I started thinking about optimisation. The decision was to
extend text search with trigram index(pg_trgm extension) together with GIN index.
After it was tested by EXPLAIN ANALYZE the execution time 
decreased from 2172 ms to 288 ms.
This steps, refactoring and other small improvements took me another 5-6 hours.


Decisions made during the implementation:
I decided not to implement field updates for changed posts because detecting edits by HTML scraping 
requires the scanning process of old posts on repeat. And also it will not garantee the last and 
the correct version of this updates.
There are other better approaches to implement such functionality such as Telegram Client API 
that can provide edits updates. And due to requirement, the app should collect posts without Telegram API.

Searching method: I used Postgresql pg_trgm extension together with GIN index. I tested the query using EXPLAIN ANALYZE, 
and the execution time decreased from approximately 2172 ms to 288 ms. Also I used paggination 
that is implemented by splitting the search results into pages of 20 posts. 
The server receives the filters, which are applied at the database level, and then returns the results to the frontend.
Other search approaches I considered:
B-tree index - not suitable for substring searches like '%word%'.
Postgresql FullTextSearch – better for searching whole words, but not ideal for matching parts of words.
Elasticsearch – overengeneering in scope of the task.
Vector search (semantic search) – overengeneering in scope of the task.
Simple ILIKE without an index – EXPLAIN ANALYZE showed better results in testing when using index approach (postgreSQL decides whether to use the index 
or seq scan depending on the query and the estimated cost).


I used raw SQL `ON CONFLICT ("ChannelTag", "MessageId") DO NOTHING` to avoid duplicate posts. 
PostgreSQL just skips this post without any error.
If I used regular EF Core approach to insert posts, I could get an error when a post already exists.


`Retries with backoff` implemented with maximum of 3 attempts. 
If request fails because of network error, timeout, HTTP 429, or server error (5xx), 
the application tries again after the short delay.
It also checks the Retry-After header to understand how long it should wait before trying again.


If there were 10000 channels in current implementaion, then there were 10000 async operations to parse each channel. 
It doesn't mean that this quantity of requests would be sent at the same time. I used a shared SemaphoreSlim to allow only one HTTP request at time, 
with at least a one second delay between requests. This helps to avoid sending too many requests to Telegram and reduces the risk of HTTP 429 errors.
This scenario with many channels can overload the application causing slow performance. Also the parsing of new messages could be blocked for a long time.
To solve this issue I would probably limit the quantity of concurrent channels.
The quantity can be calculated from the performance tests(CPU and memory usage, database performance, and the number of failed requests).
Also I would consider using RabbitMq or some close custom implementation of queues by using language capabilities.
I would implement the task by following the producer-consumer pattern.