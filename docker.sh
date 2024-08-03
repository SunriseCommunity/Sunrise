docker image build -t redis-sunrise .
docker container run -d -p 6379:6379 redis-sunrise
docker images
docker ps -a