/* screenshot_daemon.c - runs inside Android emulator
 * Compile: gcc -o screenshot_daemon screenshot_daemon.c
 * Usage:   screenshot_daemon [port]
 * Protocol: client sends "shot" -> receives raw screencap output (12B header + RGBA pixels)
 *           client sends "ping" -> receives "pong\n"
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <sys/wait.h>
#include <signal.h>
#include <errno.h>

static int send_screenshot(int client_fd) {
    int pipefd[2];
    if (pipe(pipefd) < 0) return -1;

    pid_t pid = fork();
    if (pid < 0) {
        close(pipefd[0]); close(pipefd[1]);
        return -1;
    }

    if (pid == 0) {
        /* child: redirect stdout to pipe write end, exec screencap */
        close(pipefd[0]);
        dup2(pipefd[1], STDOUT_FILENO);
        close(pipefd[1]);
        execl("/system/bin/screencap", "screencap", NULL);
        _exit(1);
    }

    /* parent: read from pipe read end, send to client */
    close(pipefd[1]);
    char buf[65536];
    ssize_t n;
    while ((n = read(pipefd[0], buf, sizeof(buf))) > 0) {
        ssize_t total = 0;
        while (total < n) {
            ssize_t w = send(client_fd, buf + total, n - total, 0);
            if (w <= 0) { close(pipefd[0]); waitpid(pid, NULL, 0); return -1; }
            total += w;
        }
    }
    close(pipefd[0]);
    waitpid(pid, NULL, 0);
    return 0;
}

static void handle_client(int client_fd) {
    char cmd[64];
    memset(cmd, 0, sizeof(cmd));
    ssize_t n = recv(client_fd, cmd, sizeof(cmd) - 1, 0);
    if (n <= 0) return;

    /* strip whitespace */
    while (n > 0 && (cmd[n-1] == '\n' || cmd[n-1] == '\r' || cmd[n-1] == ' '))
        cmd[--n] = '\0';

    if (strcmp(cmd, "ping") == 0) {
        send(client_fd, "pong\n", 5, 0);
    } else if (strcmp(cmd, "shot") == 0) {
        send_screenshot(client_fd);
    }
}

int main(int argc, char *argv[]) {
    int port = (argc > 1) ? atoi(argv[1]) : 19000;

    /* ignore SIGCHLD to auto-reap children */
    signal(SIGCHLD, SIG_IGN);

    int srv = socket(AF_INET, SOCK_STREAM, 0);
    if (srv < 0) { perror("socket"); return 1; }

    int opt = 1;
    setsockopt(srv, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    addr.sin_port = htons(port);

    if (bind(srv, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
        perror("bind"); return 1;
    }
    if (listen(srv, 4) < 0) {
        perror("listen"); return 1;
    }

    fprintf(stdout, "Screenshot daemon listening on port %d\n", port);
    fflush(stdout);

    while (1) {
        struct sockaddr_in cli;
        socklen_t cli_len = sizeof(cli);
        int client = accept(srv, (struct sockaddr*)&cli, &cli_len);
        if (client < 0) continue;
        handle_client(client);
        close(client);
    }
    return 0;
}
