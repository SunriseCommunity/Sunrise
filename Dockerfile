FROM redis:latest

EXPOSE 6379

VOLUME /data

CMD ["redis-server", "--appendonly", "yes"]
