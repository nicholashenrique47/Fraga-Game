#include "pch.h"
#if defined(_WIN32)
#define DLLEXPORT __declspec(dllexport)
#else
#define DLLEXPORT __attribute__((visibility("default")))
#endif

#include <cmath>

// Funções inline auxiliares para Performance (DOD)
inline float Fade(float t) {
    return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
}

inline float Lerp(float t, float a, float b) {
    return a + t * (b - a);
}

inline float Grad(int hash, float x, float y) {
    int h = hash & 3;
    float u = (h & 1) ? -x : x;
    float v = (h & 2) ? -y : y;
    return u + v;
}

// Linear Congruential Generator (LCG) determinístico
class LCG {
private:
    unsigned int state;
public:
    LCG(unsigned int seed) : state(seed) {}
    unsigned int Next() {
        state = state * 1664525 + 1013904223;
        return state;
    }
};

extern "C" {

    DLLEXPORT int InitFragaEngine() {
        return 1913;
    }

    DLLEXPORT void GenerateMapGrid(int* mapData, int width, int height, int seed) {
        // Tabela de permutação na stack (Sem alocação no heap, máximo de cache locality)
        int p[512];
        LCG lcg(seed);
        
        for (int i = 0; i < 256; i++) {
            p[i] = i;
        }
        
        // Fisher-Yates shuffle utilizando o LCG
        for (int i = 255; i > 0; i--) {
            int j = lcg.Next() % (i + 1);
            int temp = p[i];
            p[i] = p[j];
            p[j] = temp;
        }
        
        // Duplicar a tabela para evitar overflow nos índices
        for (int i = 0; i < 256; i++) {
            p[256 + i] = p[i];
        }

        // Configuracao de escala do Perlin Noise
        const float scale = 0.08f; 
        const float scale2 = 0.04f; // Segunda camada para biomas maiores
        
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = y * width + x;

                // --- 0. GERADOR DE PONTOS CHAVE ESTATICOS (WIN CONDITION) ---
                // O Farol Celta gigante deve ficar exatamente no centro
                if (x == width / 2 && y == height / 2) {
                    mapData[index] = 9; // 9 = Farol Celta
                    continue;
                }
                // Clareira ao redor do farol para ter espaco
                if (std::abs(x - width / 2) <= 3 && std::abs(y - height / 2) <= 3) {
                    mapData[index] = 0; // Chão limpo
                    continue;
                }

                // --- Noise 1 (Floresta e Clareiras) ---
                float nx = x * scale;
                float ny = y * scale;
                
                int X = static_cast<int>(std::floor(nx)) & 255;
                int Y = static_cast<int>(std::floor(ny)) & 255;
                nx -= std::floor(nx); ny -= std::floor(ny);
                float u = Fade(nx); float v = Fade(ny);
                int A = p[X] + Y; int B = p[X + 1] + Y;

                float res1 = Lerp(v, Lerp(u, Grad(p[A], nx, ny), Grad(p[B], nx - 1.0f, ny)),
                                     Lerp(u, Grad(p[A + 1], nx, ny - 1.0f), Grad(p[B + 1], nx - 1.0f, ny - 1.0f)));
                float noiseValue = (res1 + 1.0f) * 0.5f;

                // --- Noise 2 (Rios e Cavernas - Usando offset) ---
                float nx2 = (x + 1000.0f) * scale2;
                float ny2 = (y + 1000.0f) * scale2;
                
                int X2 = static_cast<int>(std::floor(nx2)) & 255;
                int Y2 = static_cast<int>(std::floor(ny2)) & 255;
                nx2 -= std::floor(nx2); ny2 -= std::floor(ny2);
                float u2 = Fade(nx2); float v2 = Fade(ny2);
                int A2 = p[X2] + Y2; int B2 = p[X2 + 1] + Y2;

                float res2 = Lerp(v2, Lerp(u2, Grad(p[A2], nx2, ny2), Grad(p[B2], nx2 - 1.0f, ny2)),
                                      Lerp(u2, Grad(p[A2 + 1], nx2, ny2 - 1.0f), Grad(p[B2 + 1], nx2 - 1.0f, ny2 - 1.0f)));
                float noiseValue2 = (res2 + 1.0f) * 0.5f;

                // --- Regras de Geracao Organica (Biomas) ---
                
                // 1. Rios (Faixas estreitas onde o ruido 2 e muito proximo de 0.5)
                if (std::abs(noiseValue2 - 0.5f) < 0.03f) {
                    mapData[index] = 7; // 7 = Agua / Rio
                    continue;
                }

                // 2. Cavernas / Rochas Densas (Onde o ruido 2 e muito alto)
                if (noiseValue2 > 0.75f) {
                    // Mescla com floresta nas bordas
                    if (noiseValue > 0.4f) {
                        mapData[index] = 8; // 8 = Rocha / Parede de Caverna
                    } else {
                        mapData[index] = 0; // Chão da caverna
                    }
                    continue;
                }

                // 3. Floresta normal
                if (noiseValue > 0.55f) {
                    mapData[index] = 1; // 1 = Floresta densa
                } 
                else if (noiseValue < 0.15f) {
                    // Centro das clareiras. Ruinas celtas raras.
                    int randVal = lcg.Next() % 1000;
                    if (randVal < 15) { 
                        mapData[index] = 2; // 2 = Ruinas
                    } else if (randVal > 980) {
                        mapData[index] = 10; // 10 = Loot Camp / Caixas abandonadas
                    } else {
                        mapData[index] = 0; // 0 = Clareira/Terreno vazio
                    }
                } 
                else {
                    mapData[index] = 0; // 0 = Clareira/Terreno vazio
                }
            }
        }
    }

    // TAREFA 6: Atualiza uma célula específica da grelha (ex: construção de parede)
    DLLEXPORT void UpdateMapCell(int* mapData, int width, int height, int x, int y, int newTileID) {
        if (x >= 0 && x < width && y >= 0 && y < height) {
            int index = y * width + x;
            mapData[index] = newTileID;
        }
    }

    // TAREFA 5: Pathfinding via Flow Field otimizado para Data-Oriented Design (Zero Allocation)
    DLLEXPORT void CalculateFlowField(int* mapData, float* vectorX, float* vectorY, unsigned short* intField, int* queue, int width, int height, int targetX, int targetY) {
        int size = width * height;

        // 1. Inicializa Campos
        for (int i = 0; i < size; ++i) {
            intField[i] = 65535; // Distância máxima inalcançável
            vectorX[i] = 0.0f;
            vectorY[i] = 0.0f;
        }

        // Se o target estiver fora, aborta
        if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) {
            return;
        }

        // 2. Integration Field (Algoritmo de propagação em amplitude / BFS)
        int targetIndex = targetY * width + targetX;
        intField[targetIndex] = 0;

        int head = 0;
        int tail = 0;
        queue[tail++] = targetIndex;

        // Vizinhança Ortogonal (Cima, Baixo, Esquerda, Direita)
        int dx[] = { 0, 0, -1, 1 };
        int dy[] = { -1, 1, 0, 0 };

        while (head < tail) {
            int curr = queue[head++];
            int cx = curr % width;
            int cy = curr / width;
            unsigned short cDist = intField[curr];

            for (int i = 0; i < 4; ++i) {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                    int nIndex = ny * width + nx;
                    
                    // ID 1 (Árvore) e ID 3 (Parede do Jogador) são obstáculos
                    if (mapData[nIndex] != 1 && mapData[nIndex] != 3) {
                        if (intField[nIndex] > cDist + 1) {
                            intField[nIndex] = cDist + 1;
                            queue[tail++] = nIndex; // Push na queue
                        }
                    }
                }
            }
        }

        // 3. Vector Field (Cálculo dos vetores apontando para a menor distância)
        // Usamos vizinhança de 8 direcções para permitir movimento diagonal
        int dx8[] = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int dy8[] = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int y = 0; y < height; ++y) {
            for (int x = 0; x < width; ++x) {
                int idx = y * width + x;
                
                // Células bloqueadas não precisam de vetores
                if (mapData[idx] == 1 || mapData[idx] == 3) continue;

                unsigned short minDist = intField[idx];
                int bestDx = 0;
                int bestDy = 0;

                for (int i = 0; i < 8; ++i) {
                    int nx = x + dx8[i];
                    int ny = y + dy8[i];
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                        int nIndex = ny * width + nx;
                        if (intField[nIndex] < minDist) {
                            minDist = intField[nIndex];
                            bestDx = dx8[i];
                            bestDy = dy8[i];
                        }
                    }
                }

                if (bestDx != 0 || bestDy != 0) {
                    // Normalização matemática do vetor 2D
                    float length = std::sqrt(static_cast<float>(bestDx * bestDx + bestDy * bestDy));
                    vectorX[idx] = static_cast<float>(bestDx) / length;
                    vectorY[idx] = static_cast<float>(bestDy) / length;
                }
            }
        }
    }

    // TAREFA 7: Gestor Físico de Hordas (Flocking & Boids) otimizado para Mobile
    DLLEXPORT void UpdateVultos(
        float* posX, float* posY, float* velX, float* velY, int count, 
        float* flowFieldX, float* flowFieldY, int width, int height,
        float deltaTime, float speed, float separationRadius, float separationWeight,
        float playerX, float playerY, float lightRadius) 
    {
        float separationRadiusSq = separationRadius * separationRadius;

        for (int i = 0; i < count; i++) {
            int gridX = (int)posX[i];
            int gridY = (int)posY[i];

            float targetVelX = 0.0f;
            float targetVelY = 0.0f;

            if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height) {
                targetVelX = flowFieldX[gridY * width + gridX];
                targetVelY = flowFieldY[gridY * width + gridX];
            }

            // BEHAVIOR TREE (Native O(1) Branching): Repulsão de Luz e Flanqueamento
            float dxPlayer = posX[i] - playerX;
            float dyPlayer = posY[i] - playerY;
            float distSqPlayer = dxPlayer * dxPlayer + dyPlayer * dyPlayer;
            float lightRadiusSq = lightRadius * lightRadius;
            float distPlayer = std::sqrt(distSqPlayer);

            // Flanqueamento basico: Vultos tentam cercar em vez de ir em linha reta
            if (distPlayer > 0.001f) {
                float tangentX = -dyPlayer / distPlayer;
                float tangentY = dxPlayer / distPlayer;
                float flankDir = (i % 2 == 0) ? 1.0f : -1.0f; // Metade vai pela esquerda, metade direita
                
                // Adiciona forca de flanqueamento baseada na distancia (mais forte de longe)
                targetVelX += tangentX * flankDir * 0.5f;
                targetVelY += tangentY * flankDir * 0.5f;
            }

            if (distSqPlayer < lightRadiusSq) {
                // Fuga rapida se entrar muito fundo na luz
                targetVelX = dxPlayer; 
                targetVelY = dyPlayer;
                speed *= 1.8f; 
            }
            else if (distPlayer < lightRadius + 3.0f) {
                // Ronda (Boundary Patrol): Ficam rodeando a borda da luz esperando apagar
                float tangentX = -dyPlayer / distPlayer;
                float tangentY = dxPlayer / distPlayer;
                float patrolDir = (i % 2 == 0) ? 1.0f : -1.0f;
                
                // Anula a aproximacao (flow field) e roda em volta
                targetVelX = tangentX * patrolDir;
                targetVelY = tangentY * patrolDir;
                speed *= 0.5f; // Andam devagar à espreita
            }

            float sepForceX = 0.0f;
            float sepForceY = 0.0f;
            int neighbors = 0;

            for (int j = 0; j < count; j++) {
                if (i == j) continue;

                float dx = posX[i] - posX[j];
                float dy = posY[i] - posY[j];
                float distSq = dx * dx + dy * dy;

                if (distSq < separationRadiusSq && distSq > 0.0001f) {
                    float dist = std::sqrt(distSq);
                    float diffX = dx / dist;
                    float diffY = dy / dist;

                    float weight = 1.0f - (dist / separationRadius);
                    sepForceX += diffX * weight;
                    sepForceY += diffY * weight;
                    neighbors++;
                }
            }

            if (neighbors > 0) {
                targetVelX += (sepForceX * separationWeight);
                targetVelY += (sepForceY * separationWeight);
            }

            float lengthSq = targetVelX * targetVelX + targetVelY * targetVelY;
            if (lengthSq > 0.0001f) {
                float invLength = 1.0f / std::sqrt(lengthSq);
                targetVelX *= invLength;
                targetVelY *= invLength;
            }

            velX[i] = Lerp(deltaTime * 5.0f, velX[i], targetVelX * speed);
            velY[i] = Lerp(deltaTime * 5.0f, velY[i], targetVelY * speed);

            posX[i] += velX[i] * deltaTime;
            posY[i] += velY[i] * deltaTime;
        }
    }

    // TAREFA 8: Leitura segura de célula do mapa para o C#
    DLLEXPORT int GetMapCell(int* mapData, int width, int height, int x, int y) {
        if (x >= 0 && x < width && y >= 0 && y < height) {
            return mapData[y * width + x];
        }
        return -1;
    }

    // TAREFA 9: IA do Trasno (Busca de barricada via BFS de profundidade limitada)
    DLLEXPORT int TrasnoGetNextMove(int* mapData, int width, int height, int startX, int startY, int* outTargetX, int* outTargetY) {
        int size = width * height;
        unsigned short* dist = new unsigned short[size];
        int* parent = new int[size];
        int* queue = new int[size];

        for (int i = 0; i < size; ++i) {
            dist[i] = 65535;
            parent[i] = -1;
        }

        int startIndex = startY * width + startX;
        dist[startIndex] = 0;
        
        int head = 0;
        int tail = 0;
        queue[tail++] = startIndex;

        int dx[] = { 0, 0, -1, 1 };
        int dy[] = { -1, 1, 0, 0 };
        
        int targetBarricadeIndex = -1;

        while (head < tail) {
            int curr = queue[head++];
            
            if (mapData[curr] == 3) { // 3 = Barricada
                targetBarricadeIndex = curr;
                break;
            }

            int cx = curr % width;
            int cy = curr / width;
            unsigned short cDist = dist[curr];

            // Mobile Opt: Profundidade limitada a 40 passos para evitar stutters pesados em A* completo
            if (cDist > 40) continue;

            for (int i = 0; i < 4; ++i) {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                    int nIndex = ny * width + nx;
                    
                    // Trasno não passa por árvores (ID 1)
                    if (mapData[nIndex] != 1) {
                        if (dist[nIndex] > cDist + 1) {
                            dist[nIndex] = cDist + 1;
                            parent[nIndex] = curr;
                            queue[tail++] = nIndex;
                        }
                    }
                }
            }
        }

        if (targetBarricadeIndex != -1) {
            int curr = targetBarricadeIndex;
            while (parent[curr] != startIndex && parent[curr] != -1) {
                curr = parent[curr];
            }
            *outTargetX = curr % width;
            *outTargetY = curr / width;
            
            delete[] dist;
            delete[] parent;
            delete[] queue;
            return 1;
        }

        delete[] dist;
        delete[] parent;
        delete[] queue;
        return 0; // Nenhuma barricada alcançável no raio imposto
    }

    // Raycasting de Bresenham super-otimizado (sem recursividade ou branches complexos)
    bool HasLineOfSight(int* mapData, int width, int height, int x0, int y0, int x1, int y1) {
        int dx = std::abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -std::abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;

        while (true) {
            if (x0 == x1 && y0 == y1) break;
            
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height) {
                int tileID = mapData[y0 * width + x0];
                if (tileID == 1 || tileID == 2) return false; // Bloqueado por árvore ou ruína
            }
            
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return true;
    }

    // TAREFA 9: Ameaças Espirituais (Meigas) - Cálculo DOD estrito com Line of Sight
    DLLEXPORT float CalculateMeigaDebuff(int* mapData, int width, int height, float* meigasX, float* meigasY, int meigaCount, float playerX, float playerY) {
        float maxDebuff = 0.0f;
        int pX = (int)playerX;
        int pY = (int)playerY;

        for (int i = 0; i < meigaCount; ++i) {
            float dx = meigasX[i] - playerX;
            float dy = meigasY[i] - playerY;
            float distSq = dx * dx + dy * dy;
            
            // Raio de influência estendido devido ao raycast
            if (distSq < 144.0f) { // Raio 12
                int mX = (int)meigasX[i];
                int mY = (int)meigasY[i];

                if (HasLineOfSight(mapData, width, height, mX, mY, pX, pY)) {
                    float debuff = (144.0f - distSq) / 144.0f;
                    if (debuff > maxDebuff) {
                        maxDebuff = debuff;
                    }
                }
            }
        }
        return maxDebuff * 15.0f; // Escala de degradacao da Sanidade
    }

    // ============================================================
    // FAUNA: IA Nativa para Animais (DOD - Structure of Arrays)
    // ============================================================

    // Cervos: Herbivoros passivos que fogem do jogador e dos lobos.
    // Movimento errante quando nao ameacados, fuga rapida quando detectam perigo.
    DLLEXPORT void UpdateDeer(
        float* posX, float* posY, float* velX, float* velY,
        int* stateFlags, // 0 = idle/wander, 1 = fleeing
        int count,
        float playerX, float playerY,
        float* wolfPosX, float* wolfPosY, int wolfCount,
        float deltaTime, float wanderSpeed, float fleeSpeed, float detectionRadius,
        int* mapData, int mapWidth, int mapHeight)
    {
        float detRadSq = detectionRadius * detectionRadius;

        for (int i = 0; i < count; ++i) {
            float threatX = 0.0f;
            float threatY = 0.0f;
            bool threatened = false;

            // Verificar ameaca do jogador
            float dpx = posX[i] - playerX;
            float dpy = posY[i] - playerY;
            float distSqP = dpx * dpx + dpy * dpy;

            if (distSqP < detRadSq && distSqP > 0.001f) {
                float invDist = 1.0f / std::sqrt(distSqP);
                threatX += dpx * invDist;
                threatY += dpy * invDist;
                threatened = true;
            }

            // Verificar ameaca dos lobos
            for (int w = 0; w < wolfCount; ++w) {
                float dwx = posX[i] - wolfPosX[w];
                float dwy = posY[i] - wolfPosY[w];
                float distSqW = dwx * dwx + dwy * dwy;

                if (distSqW < detRadSq * 0.5f && distSqW > 0.001f) {
                    float invDist = 1.0f / std::sqrt(distSqW);
                    threatX += dwx * invDist;
                    threatY += dwy * invDist;
                    threatened = true;
                }
            }

            if (threatened) {
                stateFlags[i] = 1; // Fleeing

                // Normalizar direcao de fuga
                float tLen = std::sqrt(threatX * threatX + threatY * threatY);
                if (tLen > 0.001f) {
                    threatX /= tLen;
                    threatY /= tLen;
                }

                velX[i] = Lerp(deltaTime * 8.0f, velX[i], threatX * fleeSpeed);
                velY[i] = Lerp(deltaTime * 8.0f, velY[i], threatY * fleeSpeed);
            }
            else {
                stateFlags[i] = 0; // Wander

                // Wander pseudo-aleatorio baseado na posicao (deterministic)
                float wx = std::sin(posX[i] * 0.7f + posY[i] * 1.3f + deltaTime * 100.0f) * wanderSpeed;
                float wy = std::cos(posX[i] * 1.1f + posY[i] * 0.9f + deltaTime * 100.0f) * wanderSpeed;

                velX[i] = Lerp(deltaTime * 2.0f, velX[i], wx);
                velY[i] = Lerp(deltaTime * 2.0f, velY[i], wy);
            }

            // Pathfinding: Evitar obstáculos (Água = 7, Rocha = 8, Barricada = 3, Fogueira = 4)
            int cx = static_cast<int>(posX[i]);
            int cy = static_cast<int>(posY[i]);
            float avoidX = 0.0f;
            float avoidY = 0.0f;
            bool hitObstacle = false;

            for (int oy = cy - 1; oy <= cy + 1; ++oy) {
                for (int ox = cx - 1; ox <= cx + 1; ++ox) {
                    if (ox >= 0 && ox < mapWidth && oy >= 0 && oy < mapHeight) {
                        int tile = mapData[oy * mapWidth + ox];
                        if (tile == 7 || tile == 8 || tile == 3 || tile == 4) {
                            float odx = posX[i] - (ox + 0.5f);
                            float ody = posY[i] - (oy + 0.5f);
                            float odistSq = odx * odx + ody * ody;
                            if (odistSq < 1.5f && odistSq > 0.001f) {
                                float invDist = 1.0f / std::sqrt(odistSq);
                                avoidX += odx * invDist * 5.0f;
                                avoidY += ody * invDist * 5.0f;
                                hitObstacle = true;
                            }
                        }
                    }
                }
            }

            if (hitObstacle) {
                velX[i] += avoidX * deltaTime;
                velY[i] += avoidY * deltaTime;
            }

            posX[i] += velX[i] * deltaTime;
            posY[i] += velY[i] * deltaTime;

            // Clamp bounds
            if (posX[i] < 2.0f) posX[i] = 2.0f;
            if (posX[i] > mapWidth - 2.0f) posX[i] = mapWidth - 2.0f;
            if (posY[i] < 2.0f) posY[i] = 2.0f;
            if (posY[i] > mapHeight - 2.0f) posY[i] = mapHeight - 2.0f;
        }
    }

    // Lobos: Predadores noturnos que cacam o jogador e os cervos.
    // De dia dormem (idle). A noite perseguem o alvo mais proximo.
    // Fogem da luz do lampiao (mesmo comportamento dos Vultos).
    DLLEXPORT void UpdateWolves(
        float* posX, float* posY, float* velX, float* velY,
        int* stateFlags, // 0 = sleeping, 1 = hunting, 2 = fleeing_light
        int count,
        float playerX, float playerY, float lightRadius,
        float* deerPosX, float* deerPosY, int deerCount,
        float deltaTime, float huntSpeed, float isNight,
        int* mapData, int mapWidth, int mapHeight)
    {
        float lightRadSq = lightRadius * lightRadius;

        for (int i = 0; i < count; ++i) {
            // De dia, lobos dormem
            if (isNight < 0.5f) {
                stateFlags[i] = 0;
                velX[i] = Lerp(deltaTime * 3.0f, velX[i], 0.0f);
                velY[i] = Lerp(deltaTime * 3.0f, velY[i], 0.0f);
                continue;
            }

            // Verificar se esta dentro da luz
            float dpx = posX[i] - playerX;
            float dpy = posY[i] - playerY;
            float distSqP = dpx * dpx + dpy * dpy;

            if (distSqP < lightRadSq && distSqP > 0.001f) {
                // Fugir da luz
                stateFlags[i] = 2;
                float invDist = 1.0f / std::sqrt(distSqP);
                velX[i] = Lerp(deltaTime * 6.0f, velX[i], dpx * invDist * huntSpeed * 1.3f);
                velY[i] = Lerp(deltaTime * 6.0f, velY[i], dpy * invDist * huntSpeed * 1.3f);
            }
            else {
                // Cacar: encontrar alvo mais proximo (jogador ou cervo)
                stateFlags[i] = 1;
                float closestDist = distSqP;
                float targetX = playerX;
                float targetY = playerY;

                // Verificar se algum cervo esta mais perto
                for (int d = 0; d < deerCount; ++d) {
                    float ddx = posX[i] - deerPosX[d];
                    float ddy = posY[i] - deerPosY[d];
                    float distSqD = ddx * ddx + ddy * ddy;

                    if (distSqD < closestDist) {
                        closestDist = distSqD;
                        targetX = deerPosX[d];
                        targetY = deerPosY[d];
                    }
                }

                // Mover em direcao ao alvo
                float dx = targetX - posX[i];
                float dy = targetY - posY[i];
                float dist = std::sqrt(dx * dx + dy * dy);

                if (dist > 0.5f) {
                    dx /= dist;
                    dy /= dist;
                    velX[i] = Lerp(deltaTime * 4.0f, velX[i], dx * huntSpeed);
                    velY[i] = Lerp(deltaTime * 4.0f, velY[i], dy * huntSpeed);
                }
            }

            // Pathfinding: Evitar obstáculos (Água = 7, Rocha = 8, Barricada = 3, Fogueira = 4)
            int cx = static_cast<int>(posX[i]);
            int cy = static_cast<int>(posY[i]);
            float avoidX = 0.0f;
            float avoidY = 0.0f;
            bool hitObstacle = false;

            for (int oy = cy - 1; oy <= cy + 1; ++oy) {
                for (int ox = cx - 1; ox <= cx + 1; ++ox) {
                    if (ox >= 0 && ox < mapWidth && oy >= 0 && oy < mapHeight) {
                        int tile = mapData[oy * mapWidth + ox];
                        if (tile == 7 || tile == 8 || tile == 3 || tile == 4) {
                            float odx = posX[i] - (ox + 0.5f);
                            float ody = posY[i] - (oy + 0.5f);
                            float odistSq = odx * odx + ody * ody;
                            if (odistSq < 1.5f && odistSq > 0.001f) {
                                float invDist = 1.0f / std::sqrt(odistSq);
                                avoidX += odx * invDist * 6.0f; // Lobos desviam mais forte
                                avoidY += ody * invDist * 6.0f;
                                hitObstacle = true;
                            }
                        }
                    }
                }
            }

            if (hitObstacle) {
                velX[i] += avoidX * deltaTime;
                velY[i] += avoidY * deltaTime;
            }

            posX[i] += velX[i] * deltaTime;
            posY[i] += velY[i] * deltaTime;

            // Clamp bounds
            if (posX[i] < 2.0f) posX[i] = 2.0f;
            if (posX[i] > mapWidth - 2.0f) posX[i] = mapWidth - 2.0f;
            if (posY[i] < 2.0f) posY[i] = 2.0f;
            if (posY[i] > mapHeight - 2.0f) posY[i] = mapHeight - 2.0f;
        }
    }
}