// ============================================================================
// BIOMA — Moteur WebGL 3D & Chargeur binaire glTF 2.0 (.glb) pour la Vitrine 3D
// Charge directement les modèles Blender exportés (.glb) depuis /models/biomes/,
// affiche les socles 3D et les cartes Polaroïd 3D, et gère OrbitControls + Raycasting.
// ============================================================================

window.BiomaVitrine3D = (function () {
    const instances = new Map();

    // Math 4x4 Matrices & Vectors
    function mat4Identity() {
        return new Float32Array([
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        ]);
    }

    function mat4Multiply(a, b) {
        const out = new Float32Array(16);
        for (let c = 0; c < 4; c++) {
            for (let r = 0; r < 4; r++) {
                out[c * 4 + r] =
                    a[0 * 4 + r] * b[c * 4 + 0] +
                    a[1 * 4 + r] * b[c * 4 + 1] +
                    a[2 * 4 + r] * b[c * 4 + 2] +
                    a[3 * 4 + r] * b[c * 4 + 3];
            }
        }
        return out;
    }

    function mat4Perspective(fovyRad, aspect, near, far) {
        const f = 1.0 / Math.tan(fovyRad / 2);
        const nf = 1 / (near - far);
        return new Float32Array([
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) * nf, -1,
            0, 0, (2 * far * near) * nf, 0
        ]);
    }

    function mat4LookAt(eye, center, up) {
        let zx = eye[0] - center[0], zy = eye[1] - center[1], zz = eye[2] - center[2];
        let len = Math.hypot(zx, zy, zz) || 1;
        zx /= len; zy /= len; zz /= len;

        let xx = up[1] * zz - up[2] * zy;
        let xy = up[2] * zx - up[0] * zz;
        let xz = up[0] * zy - up[1] * zx;
        len = Math.hypot(xx, xy, xz) || 1;
        xx /= len; xy /= len; xz /= len;

        const yx = zy * xz - zz * xy;
        const yy = zz * xx - zx * xz;
        const yz = zx * xy - zy * xx;

        return new Float32Array([
            xx, yx, zx, 0,
            xy, yy, zy, 0,
            xz, yz, zz, 0,
            -(xx * eye[0] + xy * eye[1] + xz * eye[2]),
            -(yx * eye[0] + yy * eye[1] + yz * eye[2]),
            -(zx * eye[0] + zy * eye[1] + zz * eye[2]),
            1
        ]);
    }

    function mat4FromTRS(t, q, s) {
        const tx = t ? t[0] : 0, ty = t ? t[1] : 0, tz = t ? t[2] : 0;
        const qx = q ? q[0] : 0, qy = q ? q[1] : 0, qz = q ? q[2] : 0, qw = q ? q[3] : 1;
        const sx = s ? s[0] : 1, sy = s ? s[1] : 1, sz = s ? s[2] : 1;

        const x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
        const xx = qx * x2, xy = qx * y2, xz = qx * z2;
        const yy = qy * y2, yz = qy * z2, zz = qz * z2;
        const wx = qw * x2, wy = qw * y2, wz = qw * z2;

        return new Float32Array([
            (1 - (yy + zz)) * sx, (xy + wz) * sx,       (xz - wy) * sx,       0,
            (xy - wz) * sy,       (1 - (xx + zz)) * sy, (yz + wx) * sy,       0,
            (xz + wy) * sz,       (yz - wx) * sz,       (1 - (xx + yy)) * sz, 0,
            tx,                   ty,                   tz,                   1
        ]);
    }

    function projectPointToScreen(pt, viewProj, width, height) {
        const x = pt[0], y = pt[1], z = pt[2];
        const cx = viewProj[0] * x + viewProj[4] * y + viewProj[8] * z + viewProj[12];
        const cy = viewProj[1] * x + viewProj[5] * y + viewProj[9] * z + viewProj[13];
        const cz = viewProj[2] * x + viewProj[6] * y + viewProj[10] * z + viewProj[14];
        const cw = viewProj[3] * x + viewProj[7] * y + viewProj[11] * z + viewProj[15];
        if (cw <= 0.05) return null;
        const ndcX = cx / cw;
        const ndcY = cy / cw;
        return {
            x: (ndcX * 0.5 + 0.5) * width,
            y: (1.0 - (ndcY * 0.5 + 0.5)) * height,
            depth: cz / cw
        };
    }

    // Invert 4x4 matrix for raycasting
    function mat4Invert(m) {
        const inv = new Float32Array(16);
        inv[0] = m[5]*m[10]*m[15] - m[5]*m[11]*m[14] - m[9]*m[6]*m[15] + m[9]*m[7]*m[14] + m[13]*m[6]*m[11] - m[13]*m[7]*m[10];
        inv[4] = -m[4]*m[10]*m[15] + m[4]*m[11]*m[14] + m[8]*m[6]*m[15] - m[8]*m[7]*m[14] - m[12]*m[6]*m[11] + m[12]*m[7]*m[10];
        inv[8] = m[4]*m[9]*m[15] - m[4]*m[11]*m[13] - m[8]*m[5]*m[15] + m[8]*m[7]*m[13] + m[12]*m[5]*m[11] - m[12]*m[7]*m[9];
        inv[12] = -m[4]*m[9]*m[14] + m[4]*m[10]*m[13] + m[8]*m[5]*m[14] - m[8]*m[6]*m[13] - m[12]*m[5]*m[10] + m[12]*m[6]*m[9];
        inv[1] = -m[1]*m[10]*m[15] + m[1]*m[11]*m[14] + m[9]*m[2]*m[15] - m[9]*m[3]*m[14] - m[13]*m[2]*m[11] + m[13]*m[3]*m[10];
        inv[5] = m[0]*m[10]*m[15] - m[0]*m[11]*m[14] - m[8]*m[2]*m[15] + m[8]*m[3]*m[14] + m[12]*m[2]*m[11] - m[12]*m[3]*m[10];
        inv[9] = -m[0]*m[9]*m[15] + m[0]*m[11]*m[13] + m[8]*m[1]*m[15] - m[8]*m[3]*m[13] - m[12]*m[1]*m[11] + m[12]*m[3]*m[9];
        inv[13] = m[0]*m[9]*m[14] - m[0]*m[10]*m[13] - m[8]*m[1]*m[14] + m[8]*m[2]*m[13] + m[12]*m[1]*m[10] - m[12]*m[2]*m[9];
        inv[2] = m[1]*m[6]*m[15] - m[1]*m[7]*m[14] - m[5]*m[2]*m[15] + m[5]*m[3]*m[14] + m[13]*m[2]*m[7] - m[13]*m[3]*m[6];
        inv[6] = -m[0]*m[6]*m[15] + m[0]*m[7]*m[14] + m[4]*m[2]*m[15] - m[4]*m[3]*m[14] - m[12]*m[2]*m[7] + m[12]*m[3]*m[6];
        inv[10] = m[0]*m[5]*m[15] - m[0]*m[7]*m[13] - m[4]*m[1]*m[15] + m[4]*m[3]*m[13] + m[12]*m[1]*m[7] - m[12]*m[3]*m[5];
        inv[14] = -m[0]*m[5]*m[14] + m[0]*m[6]*m[13] + m[4]*m[1]*m[14] - m[4]*m[2]*m[13] - m[12]*m[1]*m[6] + m[12]*m[2]*m[5];
        inv[3] = -m[1]*m[6]*m[11] + m[1]*m[7]*m[10] + m[5]*m[2]*m[11] - m[5]*m[3]*m[10] - m[9]*m[2]*m[7] + m[9]*m[3]*m[6];
        inv[7] = m[0]*m[6]*m[11] - m[0]*m[7]*m[10] - m[4]*m[2]*m[11] + m[4]*m[3]*m[10] + m[8]*m[2]*m[7] - m[8]*m[3]*m[6];
        inv[11] = -m[0]*m[5]*m[11] + m[0]*m[7]*m[9] + m[4]*m[1]*m[11] - m[4]*m[3]*m[9] - m[8]*m[1]*m[7] + m[8]*m[3]*m[5];
        inv[15] = m[0]*m[5]*m[10] - m[0]*m[6]*m[9] - m[4]*m[1]*m[10] + m[4]*m[2]*m[9] + m[8]*m[1]*m[6] - m[8]*m[2]*m[5];

        let det = m[0]*inv[0] + m[1]*inv[4] + m[2]*inv[8] + m[3]*inv[12];
        if (!det) return null;
        det = 1.0 / det;
        for (let i = 0; i < 16; i++) inv[i] *= det;
        return inv;
    }

    function unproject(ndcX, ndcY, ndcZ, invViewProj) {
        const x = invViewProj[0]*ndcX + invViewProj[4]*ndcY + invViewProj[8]*ndcZ + invViewProj[12];
        const y = invViewProj[1]*ndcX + invViewProj[5]*ndcY + invViewProj[9]*ndcZ + invViewProj[13];
        const z = invViewProj[2]*ndcX + invViewProj[6]*ndcY + invViewProj[10]*ndcZ + invViewProj[14];
        const w = invViewProj[3]*ndcX + invViewProj[7]*ndcY + invViewProj[11]*ndcZ + invViewProj[15] || 1.0;
        return [x / w, y / w, z / w];
    }

    // Geometry builders for Pedestals & Polaroid 3D Cards
    function createCylinderGeometry(radiusTop, radiusBottom, height, segments) {
        const positions = [];
        const normals = [];
        const uvs = [];
        const indices = [];
        const halfH = height / 2;

        // Side wall
        for (let i = 0; i <= segments; i++) {
            const u = i / segments;
            const angle = u * Math.PI * 2;
            const c = Math.cos(angle), s = Math.sin(angle);

            positions.push(c * radiusTop, halfH, s * radiusTop);
            normals.push(c, 0.15, s);
            uvs.push(u, 1);

            positions.push(c * radiusBottom, -halfH, s * radiusBottom);
            normals.push(c, 0.15, s);
            uvs.push(u, 0);
        }

        for (let i = 0; i < segments; i++) {
            const a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            indices.push(a, b, c, b, d, c);
        }

        // Top cap
        const topCenterIdx = positions.length / 3;
        positions.push(0, halfH, 0);
        normals.push(0, 1, 0);
        uvs.push(0.5, 0.5);

        for (let i = 0; i <= segments; i++) {
            const u = i / segments;
            const angle = u * Math.PI * 2;
            const c = Math.cos(angle), s = Math.sin(angle);
            positions.push(c * radiusTop, halfH, s * radiusTop);
            normals.push(0, 1, 0);
            uvs.push(0.5 + c * 0.5, 0.5 + s * 0.5);
        }

        for (let i = 0; i < segments; i++) {
            indices.push(topCenterIdx, topCenterIdx + 1 + i + 1, topCenterIdx + 1 + i);
        }

        return {
            positions: new Float32Array(positions),
            normals: new Float32Array(normals),
            uvs: new Float32Array(uvs),
            indices: new Uint16Array(indices)
        };
    }

    function createBoxGeometry(w, h, d) {
        const hw = w / 2, hh = h / 2, hd = d / 2;
        // Front face (+Z) has UV [0..1, 0..1] for the Polaroid texture
        const positions = new Float32Array([
            // Front (+Z)
            -hw, -hh,  hd,   hw, -hh,  hd,   hw,  hh,  hd,  -hw,  hh,  hd,
            // Back (-Z)
             hw, -hh, -hd,  -hw, -hh, -hd,  -hw,  hh, -hd,   hw,  hh, -hd,
            // Top (+Y)
            -hw,  hh,  hd,   hw,  hh,  hd,   hw,  hh, -hd,  -hw,  hh, -hd,
            // Bottom (-Y)
            -hw, -hh, -hd,   hw, -hh, -hd,   hw, -hh,  hd,  -hw, -hh,  hd,
            // Right (+X)
             hw, -hh,  hd,   hw, -hh, -hd,   hw,  hh, -hd,   hw,  hh,  hd,
            // Left (-X)
            -hw, -hh, -hd,  -hw, -hh,  hd,  -hw,  hh,  hd,  -hw,  hh, -hd
        ]);
        const normals = new Float32Array([
            0, 0, 1,  0, 0, 1,  0, 0, 1,  0, 0, 1,
            0, 0,-1,  0, 0,-1,  0, 0,-1,  0, 0,-1,
            0, 1, 0,  0, 1, 0,  0, 1, 0,  0, 1, 0,
            0,-1, 0,  0,-1, 0,  0,-1, 0,  0,-1, 0,
            1, 0, 0,  1, 0, 0,  1, 0, 0,  1, 0, 0,
           -1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0
        ]);
        const uvs = new Float32Array([
            // Front face mapped to full Polaroid canvas
            0, 1,  1, 1,  1, 0,  0, 0,
            // Back face mapped to full Polaroid canvas too so card is visible from both sides
            0, 1,  1, 1,  1, 0,  0, 0,
            // Edges mapped to top-left white border pixel (0.02, 0.02)
            0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02,
            0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02,
            0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02,
            0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.02
        ]);
        const indices = new Uint16Array([
             0,  1,  2,   0,  2,  3,
             4,  5,  6,   4,  6,  7,
             8,  9, 10,   8, 10, 11,
            12, 13, 14,  12, 14, 15,
            16, 17, 18,  16, 18, 19,
            20, 21, 22,  20, 22, 23
        ]);
        return { positions, normals, uvs, indices };
    }

    // Parse binary .glb file into WebGL buffers + node draw list
    async function loadGlbModel(gl, url) {
        const resp = await fetch(url, { cache: "no-cache" });
        if (!resp.ok) {
            throw new Error(`HTTP ${resp.status} sur ${url}`);
        }
        const arrayBuffer = await resp.arrayBuffer();
        const dv = new DataView(arrayBuffer);

        const magic = dv.getUint32(0, true);
        if (magic !== 0x46546C67) {
            throw new Error("Format GLB invalide (en-tête glTF manquant).");
        }

        const jsonChunkLength = dv.getUint32(12, true);
        const jsonChunkType = dv.getUint32(16, true);
        if (jsonChunkType !== 0x4E4F534A) {
            throw new Error("Chunk JSON GLB introuvable.");
        }

        const jsonBytes = new Uint8Array(arrayBuffer, 20, jsonChunkLength);
        const gltf = JSON.parse(new TextDecoder("utf-8").decode(jsonBytes));

        const binChunkOffset = 20 + jsonChunkLength;
        const binChunkLength = dv.getUint32(binChunkOffset, true);
        const binBufferOffset = binChunkOffset + 8;

        function getAccessorArray(accessorIdx) {
            const acc = gltf.accessors[accessorIdx];
            const bv = gltf.bufferViews[acc.bufferView];
            const byteOffset = binBufferOffset + (bv.byteOffset || 0) + (acc.byteOffset || 0);
            const numComponents = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 }[acc.type] || 1;
            const length = acc.count * numComponents;

            switch (acc.componentType) {
                case 5126: return new Float32Array(arrayBuffer, byteOffset, length);
                case 5123: return new Uint16Array(arrayBuffer, byteOffset, length);
                case 5125: return new Uint32Array(arrayBuffer, byteOffset, length);
                case 5121: return new Uint8Array(arrayBuffer, byteOffset, length);
                default:   return new Float32Array(arrayBuffer, byteOffset, length);
            }
        }

        const materials = (gltf.materials || []).map(m => {
            const name = m.name || "Material";
            const defaultFactor = name.toLowerCase().includes("snow") || name.toLowerCase().includes("ice")
                ? [0.92, 0.95, 1.0, 1.0]
                : [0.72, 0.76, 0.72, 1.0];
            const factor = m.pbrMetallicRoughness?.baseColorFactor || defaultFactor;
            // Convert linear PBR color from Blender to vibrant sRGB display color
            const r = Math.min(1.0, Math.pow(factor[0], 0.65) * 1.15);
            const g = Math.min(1.0, Math.pow(factor[1], 0.65) * 1.15);
            const b = Math.min(1.0, Math.pow(factor[2], 0.65) * 1.15);
            return {
                name,
                color: [r, g, b, factor[3] ?? 1.0]
            };
        });

        const gpuMeshes = (gltf.meshes || []).map(mesh => {
            const prims = (mesh.primitives || []).map(prim => {
                const posArr = getAccessorArray(prim.attributes.POSITION);
                const normArr = prim.attributes.NORMAL !== undefined
                    ? getAccessorArray(prim.attributes.NORMAL)
                    : new Float32Array(posArr.length);
                const uvArr = prim.attributes.TEXCOORD_0 !== undefined
                    ? getAccessorArray(prim.attributes.TEXCOORD_0)
                    : new Float32Array((posArr.length / 3) * 2);
                const idxArr = prim.indices !== undefined
                    ? getAccessorArray(prim.indices)
                    : null;

                const posBuf = gl.createBuffer();
                gl.bindBuffer(gl.ARRAY_BUFFER, posBuf);
                gl.bufferData(gl.ARRAY_BUFFER, posArr, gl.STATIC_DRAW);

                const normBuf = gl.createBuffer();
                gl.bindBuffer(gl.ARRAY_BUFFER, normBuf);
                gl.bufferData(gl.ARRAY_BUFFER, normArr, gl.STATIC_DRAW);

                const uvBuf = gl.createBuffer();
                gl.bindBuffer(gl.ARRAY_BUFFER, uvBuf);
                gl.bufferData(gl.ARRAY_BUFFER, uvArr, gl.STATIC_DRAW);

                let idxBuf = null;
                let idxType = gl.UNSIGNED_SHORT;
                let idxCount = posArr.length / 3;

                if (idxArr) {
                    idxBuf = gl.createBuffer();
                    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, idxBuf);
                    if (idxArr instanceof Uint32Array) {
                        const uint16 = new Uint16Array(idxArr.length);
                        for (let i = 0; i < idxArr.length; i++) uint16[i] = idxArr[i];
                        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, uint16, gl.STATIC_DRAW);
                    } else {
                        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, idxArr, gl.STATIC_DRAW);
                    }
                    idxCount = idxArr.length;
                }

                const mat = materials[prim.material] || { name: "Default", color: [0.35, 0.55, 0.38, 1.0] };
                return { posBuf, normBuf, uvBuf, idxBuf, idxCount, idxType, color: mat.color, matName: mat.name, posArr, idxArr };
            });
            return { name: mesh.name || "Mesh", primitives: prims };
        });

        const drawCalls = [];
        const nodes = gltf.nodes || [];

        function traverseNode(nodeIdx, parentMatrix) {
            const node = nodes[nodeIdx];
            if (!node) return;
            const localMat = node.matrix
                ? new Float32Array(node.matrix)
                : mat4FromTRS(node.translation, node.rotation, node.scale);
            const worldMat = mat4Multiply(parentMatrix, localMat);

            if (node.mesh !== undefined && gpuMeshes[node.mesh]) {
                drawCalls.push({
                    nodeName: node.name || `Node_${nodeIdx}`,
                    worldMatrix: worldMat,
                    mesh: gpuMeshes[node.mesh]
                });
            }

            if (node.children) {
                for (const childIdx of node.children) {
                    traverseNode(childIdx, worldMat);
                }
            }
        }

        const sceneIdx = gltf.scene || 0;
        const rootNodes = gltf.scenes?.[sceneIdx]?.nodes || nodes.map((_, i) => i);
        const identity = mat4Identity();
        for (const rootIdx of rootNodes) {
            traverseNode(rootIdx, identity);
        }

        // Compute world bounding box and normalize any biome size (6x6, 10x10, 20x20...) to uniform 6.6x6.6 diorama
        function transformPoint(m, x, y, z) {
            return [
                m[0] * x + m[4] * y + m[8] * z + m[12],
                m[1] * x + m[5] * y + m[9] * z + m[13],
                m[2] * x + m[6] * y + m[10] * z + m[14]
            ];
        }

        let minX = Infinity, minY = Infinity, minZ = Infinity;
        let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;

        for (const dc of drawCalls) {
            for (const prim of dc.mesh.primitives) {
                const pos = prim.posArr;
                for (let i = 0; i < pos.length; i += 3) {
                    const p = transformPoint(dc.worldMatrix, pos[i], pos[i + 1], pos[i + 2]);
                    if (p[0] < minX) minX = p[0];
                    if (p[1] < minY) minY = p[1];
                    if (p[2] < minZ) minZ = p[2];
                    if (p[0] > maxX) maxX = p[0];
                    if (p[1] > maxY) maxY = p[1];
                    if (p[2] > maxZ) maxZ = p[2];
                }
            }
        }

        if (!Number.isFinite(minX)) {
            minX = -3; maxX = 3; minY = -0.3; maxY = 1.5; minZ = -3; maxZ = 3;
        }

        const spanXZ = Math.max(maxX - minX, maxZ - minZ, 0.001);
        const normScale = 6.6 / spanXZ;
        const shiftX = -((minX + maxX) * 0.5) * normScale;
        const shiftZ = -((minZ + maxZ) * 0.5) * normScale;
        const shiftY = -0.30 - minY * normScale;
        const normMat = mat4FromTRS([shiftX, shiftY, shiftZ], null, [normScale, normScale, normScale]);

        // Extract ground/terrain triangles in normalized world space for accurate pedestal Y snapping & click raycasting
        const groundTriangles = [];
        for (const dc of drawCalls) {
            dc.worldMatrix = mat4Multiply(normMat, dc.worldMatrix);
            const nLower = dc.nodeName.toLowerCase();
            const isProp = nLower.includes("pine") || nLower.includes("tree") || nLower.includes("cactus");
            if (isProp) continue;

            for (const prim of dc.mesh.primitives) {
                const mLower = (prim.matName || "").toLowerCase();
                if (mLower.includes("leaf") || mLower.includes("branch") || mLower.includes("log") || mLower.includes("cactus")) {
                    continue;
                }
                const pos = prim.posArr;
                const idx = prim.idxArr;
                const triCount = idx ? Math.floor(idx.length / 3) : Math.floor(pos.length / 9);
                for (let t = 0; t < triCount; t++) {
                    const i0 = idx ? idx[t * 3] : t * 3;
                    const i1 = idx ? idx[t * 3 + 1] : t * 3 + 1;
                    const i2 = idx ? idx[t * 3 + 2] : t * 3 + 2;
                    const v0 = transformPoint(dc.worldMatrix, pos[i0 * 3], pos[i0 * 3 + 1], pos[i0 * 3 + 2]);
                    const v1 = transformPoint(dc.worldMatrix, pos[i1 * 3], pos[i1 * 3 + 1], pos[i1 * 3 + 2]);
                    const v2 = transformPoint(dc.worldMatrix, pos[i2 * 3], pos[i2 * 3 + 1], pos[i2 * 3 + 2]);
                    groundTriangles.push([v0, v1, v2]);
                }
            }
        }

        const normMaxY = maxY * normScale + shiftY;
        const targetCenterY = Math.max(0.25, Math.min(1.4, (normMaxY - 0.30) * 0.38));

        return { drawCalls, groundTriangles, targetCenterY, nodeCount: nodes.length, meshCount: gpuMeshes.length };
    }

    function rayTriangleIntersect(orig, dir, v0, v1, v2) {
        const EPS = 1e-6;
        const e1 = [v1[0] - v0[0], v1[1] - v0[1], v1[2] - v0[2]];
        const e2 = [v2[0] - v0[0], v2[1] - v0[1], v2[2] - v0[2]];
        const h = [
            dir[1] * e2[2] - dir[2] * e2[1],
            dir[2] * e2[0] - dir[0] * e2[2],
            dir[0] * e2[1] - dir[1] * e2[0]
        ];
        const a = e1[0] * h[0] + e1[1] * h[1] + e1[2] * h[2];
        if (a > -EPS && a < EPS) return null;
        const f = 1.0 / a;
        const s = [orig[0] - v0[0], orig[1] - v0[1], orig[2] - v0[2]];
        const u = f * (s[0] * h[0] + s[1] * h[1] + s[2] * h[2]);
        if (u < 0.0 || u > 1.0) return null;
        const q = [
            s[1] * e1[2] - s[2] * e1[1],
            s[2] * e1[0] - s[0] * e1[2],
            s[0] * e1[1] - s[1] * e1[0]
        ];
        const v = f * (dir[0] * q[0] + dir[1] * q[1] + dir[2] * q[2]);
        if (v < 0.0 || u + v > 1.0) return null;
        const t = f * (e2[0] * q[0] + e2[1] * q[1] + e2[2] * q[2]);
        return t > EPS ? t : null;
    }

    function sampleTerrainY(model, x, z, fallbackY) {
        if (!model || !model.groundTriangles || model.groundTriangles.length === 0) {
            return fallbackY ?? 0.30;
        }
        const orig = [x, 20.0, z];
        const dir = [0.0, -1.0, 0.0];
        let bestY = -Infinity;
        for (const tri of model.groundTriangles) {
            const t = rayTriangleIntersect(orig, dir, tri[0], tri[1], tri[2]);
            if (t !== null) {
                const hitY = 20.0 - t;
                if (hitY > bestY) bestY = hitY;
            }
        }
        return bestY > -10.0 ? bestY : (fallbackY ?? 0.30);
    }

    function raycastBiomeGround(model, nearPt, farPt) {
        if (!model || !model.groundTriangles || model.groundTriangles.length === 0) {
            return null;
        }
        const dir = [farPt[0] - nearPt[0], farPt[1] - nearPt[1], farPt[2] - nearPt[2]];
        const len = Math.hypot(dir[0], dir[1], dir[2]) || 1;
        dir[0] /= len; dir[1] /= len; dir[2] /= len;

        let closestT = Infinity;
        for (const tri of model.groundTriangles) {
            const t = rayTriangleIntersect(nearPt, dir, tri[0], tri[1], tri[2]);
            if (t !== null && t < closestT) {
                closestT = t;
            }
        }
        if (!Number.isFinite(closestT)) return null;
        return [
            nearPt[0] + dir[0] * closestT,
            nearPt[1] + dir[1] * closestT,
            nearPt[2] + dir[2] * closestT
        ];
    }

    function createGpuMeshFromData(gl, geom) {
        const posBuf = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, posBuf);
        gl.bufferData(gl.ARRAY_BUFFER, geom.positions, gl.STATIC_DRAW);

        const normBuf = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, normBuf);
        gl.bufferData(gl.ARRAY_BUFFER, geom.normals, gl.STATIC_DRAW);

        const uvBuf = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, uvBuf);
        gl.bufferData(gl.ARRAY_BUFFER, geom.uvs, gl.STATIC_DRAW);

        const idxBuf = gl.createBuffer();
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, idxBuf);
        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, geom.indices, gl.STATIC_DRAW);

        return {
            posBuf,
            normBuf,
            uvBuf,
            idxBuf,
            idxCount: geom.indices.length,
            idxType: gl.UNSIGNED_SHORT
        };
    }

    function rarityTheme(rarity) {
        const r = (rarity || "").toLowerCase();
        if (r.includes("legendaire")) {
            return { ring: [0.96, 0.78, 0.32], hex: "#F3C969", badgeBg: "#B7791F", label: "LÉGENDAIRE" };
        }
        if (r.includes("rare")) {
            return { ring: [0.75, 0.52, 0.98], hex: "#C084FC", badgeBg: "#6B21A8", label: "RARE" };
        }
        if (r.includes("peu")) {
            return { ring: [0.34, 0.85, 0.56], hex: "#4ADE80", badgeBg: "#15803D", label: "PEU COMMUNE" };
        }
        return { ring: [0.62, 0.76, 0.68], hex: "#94A3B8", badgeBg: "#334155", label: "COMMUNE" };
    }

    function buildPolaroidTexture(gl, card) {
        const tex = gl.createTexture();
        const canvas = document.createElement("canvas");
        canvas.width = 512;
        canvas.height = 512;
        const ctx = canvas.getContext("2d");

        const theme = rarityTheme(card.rarity);

        function paintCard(imgElement) {
            // Polaroid paper frame
            ctx.fillStyle = "#F7F3E8";
            ctx.fillRect(0, 0, 512, 512);

            // Rarity outer border
            ctx.strokeStyle = theme.hex;
            ctx.lineWidth = 12;
            ctx.strokeRect(6, 6, 500, 500);

            // Photo area background
            ctx.fillStyle = "#12261A";
            ctx.fillRect(24, 24, 464, 326);

            if (imgElement) {
                const iw = imgElement.naturalWidth || 464;
                const ih = imgElement.naturalHeight || 326;
                const scale = Math.max(464 / iw, 326 / ih);
                const sw = 464 / scale;
                const sh = 326 / scale;
                const sx = (iw - sw) / 2;
                const sy = (ih - sh) / 2;
                ctx.save();
                ctx.beginPath();
                ctx.rect(24, 24, 464, 326);
                ctx.clip();
                ctx.drawImage(imgElement, sx, sy, sw, sh, 24, 24, 464, 326);
                ctx.restore();
            } else {
                const grad = ctx.createLinearGradient(24, 24, 488, 350);
                grad.addColorStop(0, "#153222");
                grad.addColorStop(1, "#0B1B12");
                ctx.fillStyle = grad;
                ctx.fillRect(24, 24, 464, 326);

                ctx.fillStyle = theme.hex;
                ctx.font = "bold 30px Georgia, serif";
                ctx.textAlign = "center";
                ctx.fillText(card.commonName || "Espèce", 256, 182);
                ctx.fillStyle = "#9FB8A8";
                ctx.font = "italic 18px sans-serif";
                ctx.fillText(card.scientificName || "", 256, 214);
            }

            // Rarity badge top-right of photo
            ctx.fillStyle = theme.badgeBg;
            ctx.fillRect(306, 36, 168, 32);
            ctx.strokeStyle = theme.hex;
            ctx.lineWidth = 2;
            ctx.strokeRect(306, 36, 168, 32);
            ctx.fillStyle = "#FFFFFF";
            ctx.font = "bold 16px sans-serif";
            ctx.textAlign = "center";
            ctx.fillText(theme.label, 390, 58);

            // Common Name
            ctx.fillStyle = "#14261C";
            ctx.font = "bold 28px Georgia, serif";
            ctx.textAlign = "left";
            const rawName = card.commonName || "Animal";
            const title = rawName.length > 23 ? rawName.substring(0, 22) + "…" : rawName;
            ctx.fillText(title, 28, 392);

            // Scientific Name
            ctx.fillStyle = "#4A6656";
            ctx.font = "italic 19px sans-serif";
            ctx.fillText(card.scientificName || "", 28, 422);

            // Divider
            ctx.strokeStyle = "#D5CEBE";
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(28, 442);
            ctx.lineTo(484, 442);
            ctx.stroke();

            // Stats bar
            ctx.fillStyle = "#1E3A2B";
            ctx.font = "bold 19px sans-serif";
            ctx.fillText(`ATK ${card.currentAttack}  ·  DEF ${card.currentDefense}  ·  Niv. ${card.level || 1}`, 28, 476);

            gl.bindTexture(gl.TEXTURE_2D, tex);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
            gl.generateMipmap(gl.TEXTURE_2D);
        }

        paintCard(null);

        if (card.imageUrl) {
            const img = new Image();
            img.onload = () => paintCard(img);
            img.onerror = () => {
                console.warn("[BiomaVitrine3D] Impossible de charger l'image:", card.imageUrl);
            };
            const url = card.imageUrl;
            img.src = (url.startsWith("http://") || url.startsWith("https://"))
                ? `/api/showcase/image-proxy?url=${encodeURIComponent(url)}`
                : url;
        }

        return tex;
    }

    async function init(canvasId, overlayId, dotNetRef, initialState) {
        dispose(canvasId);

        const canvas = document.getElementById(canvasId);
        const overlay = document.getElementById(overlayId);
        if (!canvas) return false;

        const gl = canvas.getContext("webgl", { antialias: true, alpha: false, stencil: false });
        if (!gl) return false;

        // Compile Shaders
        const vsSource = `
            attribute vec3 aPos;
            attribute vec3 aNorm;
            attribute vec2 aUv;
            uniform mat4 uModel;
            uniform mat4 uViewProj;
            varying vec3 vWorldPos;
            varying vec3 vNorm;
            varying vec2 vUv;
            void main() {
                vec4 worldPos = uModel * vec4(aPos, 1.0);
                vWorldPos = worldPos.xyz;
                vNorm = normalize(mat3(uModel[0].xyz, uModel[1].xyz, uModel[2].xyz) * aNorm);
                vUv = aUv;
                gl_Position = uViewProj * worldPos;
            }
        `;

        const fsSource = `
            precision mediump float;
            varying vec3 vWorldPos;
            varying vec3 vNorm;
            varying vec2 vUv;
            uniform vec4 uColor;
            uniform bool uUseTex;
            uniform bool uUnlit;
            uniform sampler2D uTex;
            uniform vec3 uSunDir;
            uniform vec3 uSunColor;
            uniform vec3 uSkyColor;
            uniform vec3 uGroundBounce;
            uniform vec3 uFogColor;
            uniform vec3 uCamPos;

            void main() {
                vec4 base = uUseTex ? texture2D(uTex, vUv) : uColor;
                if (uUnlit) {
                    gl_FragColor = base;
                    return;
                }

                vec3 N = normalize(vNorm);
                vec3 L = normalize(uSunDir);
                vec3 V = normalize(uCamPos - vWorldPos);
                vec3 H = normalize(L + V);

                float ndotl = max(dot(N, L), 0.0);
                float hemi = N.y * 0.5 + 0.5;
                vec3 ambient = mix(uGroundBounce, uSkyColor, hemi);

                // Subtle height shading on terrain & pine trees for richer depth
                float heightBoost = clamp((vWorldPos.y + 0.3) * 0.35, 0.0, 0.45);
                float spec = pow(max(dot(N, H), 0.0), 28.0) * 0.16;

                vec3 lit = base.rgb * (ambient + uSunColor * (ndotl * 0.85) + vec3(heightBoost * 0.12)) + vec3(spec);

                float dist = length(uCamPos - vWorldPos);
                float fogFactor = clamp((dist - 10.0) / 18.0, 0.0, 0.55);
                vec3 finalRgb = mix(lit, uFogColor, fogFactor);

                gl_FragColor = vec4(finalRgb, base.a);
            }
        `;

        function compileShader(type, src) {
            const s = gl.createShader(type);
            gl.shaderSource(s, src);
            gl.compileShader(s);
            return s;
        }

        const prog = gl.createProgram();
        gl.attachShader(prog, compileShader(gl.VERTEX_SHADER, vsSource));
        gl.attachShader(prog, compileShader(gl.FRAGMENT_SHADER, fsSource));
        gl.linkProgram(prog);
        gl.useProgram(prog);

        const locs = {
            aPos: gl.getAttribLocation(prog, "aPos"),
            aNorm: gl.getAttribLocation(prog, "aNorm"),
            aUv: gl.getAttribLocation(prog, "aUv"),
            uModel: gl.getUniformLocation(prog, "uModel"),
            uViewProj: gl.getUniformLocation(prog, "uViewProj"),
            uColor: gl.getUniformLocation(prog, "uColor"),
            uUseTex: gl.getUniformLocation(prog, "uUseTex"),
            uUnlit: gl.getUniformLocation(prog, "uUnlit"),
            uTex: gl.getUniformLocation(prog, "uTex"),
            uSunDir: gl.getUniformLocation(prog, "uSunDir"),
            uSunColor: gl.getUniformLocation(prog, "uSunColor"),
            uSkyColor: gl.getUniformLocation(prog, "uSkyColor"),
            uGroundBounce: gl.getUniformLocation(prog, "uGroundBounce"),
            uFogColor: gl.getUniformLocation(prog, "uFogColor"),
            uCamPos: gl.getUniformLocation(prog, "uCamPos")
        };

        // Shared 3D meshes for Pedestals, Diorama Base & Polaroid Cards
        const dioramaBaseMesh = createGpuMeshFromData(gl, createCylinderGeometry(4.85, 4.55, 0.24, 48));
        const pedestalBaseMesh = createGpuMeshFromData(gl, createCylinderGeometry(0.34, 0.40, 0.14, 28));
        const pedestalTopMesh = createGpuMeshFromData(gl, createCylinderGeometry(0.27, 0.30, 0.08, 28));
        const pedestalRingMesh = createGpuMeshFromData(gl, createCylinderGeometry(0.32, 0.32, 0.03, 28));
        const polaroidCardMesh = createGpuMeshFromData(gl, createBoxGeometry(0.70, 0.84, 0.035));
        const fireflyMesh = createGpuMeshFromData(gl, createBoxGeometry(0.04, 0.04, 0.04));

        // 24 ambient 3D fireflies / pollen particles floating in the forest
        const fireflies = Array.from({ length: 24 }, (_, i) => ({
            x: (Math.sin(i * 17.3) * 2.5),
            y: 0.55 + (i % 5) * 0.28,
            z: (Math.cos(i * 23.1) * 2.5),
            speed: 0.6 + (i % 4) * 0.25,
            phase: i * 1.1
        }));

        const state = {
            canvas,
            overlay,
            gl,
            prog,
            locs,
            dotNetRef,
            glbUrl: null,
            biomeModel: null,
            pedestals: [],
            cardTextures: new Map(),
            selectedSlotIndex: 0,
            placementMode: false,
            autoRotate: false,
            atmosphere: "day",
            camera: {
                theta: 0.62,
                phi: 0.98,
                radius: 6.8,
                target: [0.0, 0.45, 0.15],
                goalTheta: 0.62,
                goalPhi: 0.98,
                goalRadius: 6.8,
                goalTarget: [0.0, 0.45, 0.15]
            },
            lastViewProj: mat4Identity(),
            eyePos: [0, 4, 6],
            disposed: false,
            animId: 0
        };

        instances.set(canvasId, state);

        // OrbitControls mouse & touch interactions
        let isDragging = false;
        let dragButton = 0;
        let startX = 0, startY = 0;
        let lastX = 0, lastY = 0;
        let pointerMoved = false;

        canvas.addEventListener("contextmenu", e => e.preventDefault());

        canvas.addEventListener("pointerdown", e => {
            isDragging = true;
            dragButton = e.button;
            startX = lastX = e.clientX;
            startY = lastY = e.clientY;
            pointerMoved = false;
            canvas.setPointerCapture(e.pointerId);
        });

        canvas.addEventListener("pointermove", e => {
            if (!isDragging) return;
            const dx = e.clientX - lastX;
            const dy = e.clientY - lastY;
            if (Math.hypot(e.clientX - startX, e.clientY - startY) > 5) {
                pointerMoved = true;
            }
            lastX = e.clientX;
            lastY = e.clientY;

            if (dragButton === 0 && !e.shiftKey) {
                // Orbit 360°
                state.camera.goalTheta -= dx * 0.0075;
                state.camera.goalPhi = Math.max(0.22, Math.min(1.46, state.camera.goalPhi - dy * 0.0065));
            } else {
                // Pan
                const panFactor = state.camera.radius * 0.0014;
                const rightX = Math.cos(state.camera.theta);
                const rightZ = -Math.sin(state.camera.theta);
                state.camera.goalTarget[0] = Math.max(-2.5, Math.min(2.5, state.camera.goalTarget[0] - dx * rightX * panFactor));
                state.camera.goalTarget[2] = Math.max(-2.5, Math.min(2.5, state.camera.goalTarget[2] - dx * rightZ * panFactor));
                state.camera.goalTarget[1] = Math.max(-0.2, Math.min(2.0, state.camera.goalTarget[1] + dy * panFactor));
            }
        });

        canvas.addEventListener("pointerup", e => {
            if (!isDragging) return;
            isDragging = false;
            try { canvas.releasePointerCapture(e.pointerId); } catch {}

            if (!pointerMoved && dragButton === 0) {
                handleCanvasClick(state, e);
            }
        });

        canvas.addEventListener("wheel", e => {
            e.preventDefault();
            state.camera.goalRadius = Math.max(2.6, Math.min(13.5, state.camera.goalRadius + e.deltaY * 0.0045));
        }, { passive: false });

        await updateScene(canvasId, initialState);

        function bindAndDrawMesh(mesh, worldMat, color, tex) {
            gl.uniformMatrix4fv(locs.uModel, false, worldMat);

            gl.bindBuffer(gl.ARRAY_BUFFER, mesh.posBuf);
            gl.enableVertexAttribArray(locs.aPos);
            gl.vertexAttribPointer(locs.aPos, 3, gl.FLOAT, false, 0, 0);

            gl.bindBuffer(gl.ARRAY_BUFFER, mesh.normBuf);
            gl.enableVertexAttribArray(locs.aNorm);
            gl.vertexAttribPointer(locs.aNorm, 3, gl.FLOAT, false, 0, 0);

            gl.bindBuffer(gl.ARRAY_BUFFER, mesh.uvBuf);
            gl.enableVertexAttribArray(locs.aUv);
            gl.vertexAttribPointer(locs.aUv, 2, gl.FLOAT, false, 0, 0);

            if (tex) {
                gl.uniform1i(locs.uUseTex, 1);
                gl.activeTexture(gl.TEXTURE0);
                gl.bindTexture(gl.TEXTURE_2D, tex);
                gl.uniform1i(locs.uTex, 0);
            } else {
                gl.uniform1i(locs.uUseTex, 0);
                gl.uniform4fv(locs.uColor, color || [0.5, 0.5, 0.5, 1.0]);
            }

            if (mesh.idxBuf) {
                gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, mesh.idxBuf);
                gl.drawElements(gl.TRIANGLES, mesh.idxCount, mesh.idxType, 0);
            } else {
                gl.drawArrays(gl.TRIANGLES, 0, mesh.idxCount);
            }
        }

        function renderLoop(nowMs) {
            if (state.disposed) return;
            const t = nowMs * 0.001;

            const rect = canvas.getBoundingClientRect();
            const dpr = Math.min(window.devicePixelRatio || 1, 2);
            const targetW = Math.max(300, Math.floor(rect.width * dpr));
            const targetH = Math.max(240, Math.floor(rect.height * dpr));
            if (canvas.width !== targetW || canvas.height !== targetH) {
                canvas.width = targetW;
                canvas.height = targetH;
            }

            gl.viewport(0, 0, canvas.width, canvas.height);

            // Smooth camera damping
            if (state.autoRotate && !isDragging) {
                state.camera.goalTheta += 0.0032;
            }
            state.camera.theta += (state.camera.goalTheta - state.camera.theta) * 0.14;
            state.camera.phi += (state.camera.goalPhi - state.camera.phi) * 0.14;
            state.camera.radius += (state.camera.goalRadius - state.camera.radius) * 0.14;
            for (let i = 0; i < 3; i++) {
                state.camera.target[i] += (state.camera.goalTarget[i] - state.camera.target[i]) * 0.14;
            }

            // Atmosphere presets
            let bg = [0.055, 0.125, 0.085];
            let sunDir = [0.55, 0.85, 0.45];
            let sunCol = [1.05, 0.98, 0.84];
            let skyCol = [0.38, 0.56, 0.45];
            let groundBounce = [0.16, 0.25, 0.18];

            if (state.atmosphere === "sunset") {
                bg = [0.14, 0.09, 0.08];
                sunDir = [0.82, 0.38, 0.35];
                sunCol = [1.25, 0.72, 0.42];
                skyCol = [0.52, 0.36, 0.32];
                groundBounce = [0.22, 0.15, 0.12];
            } else if (state.atmosphere === "night") {
                bg = [0.025, 0.055, 0.065];
                sunDir = [-0.35, 0.85, -0.35];
                sunCol = [0.45, 0.68, 0.92];
                skyCol = [0.16, 0.28, 0.38];
                groundBounce = [0.08, 0.14, 0.16];
            }

            gl.clearColor(bg[0], bg[1], bg[2], 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
            gl.enable(gl.DEPTH_TEST);

            const eye = [
                state.camera.target[0] + state.camera.radius * Math.sin(state.camera.phi) * Math.sin(state.camera.theta),
                state.camera.target[1] + state.camera.radius * Math.cos(state.camera.phi),
                state.camera.target[2] + state.camera.radius * Math.sin(state.camera.phi) * Math.cos(state.camera.theta)
            ];
            state.eyePos = eye;

            const proj = mat4Perspective(45 * Math.PI / 180, canvas.width / canvas.height, 0.1, 60.0);
            const view = mat4LookAt(eye, state.camera.target, [0, 1, 0]);
            const viewProj = mat4Multiply(proj, view);
            state.lastViewProj = viewProj;

            gl.useProgram(prog);
            gl.uniformMatrix4fv(locs.uViewProj, false, viewProj);
            gl.uniform3fv(locs.uSunDir, sunDir);
            gl.uniform3fv(locs.uSunColor, sunCol);
            gl.uniform3fv(locs.uSkyColor, skyCol);
            gl.uniform3fv(locs.uGroundBounce, groundBounce);
            gl.uniform3fv(locs.uFogColor, bg);
            gl.uniform3fv(locs.uCamPos, eye);
            gl.uniform1i(locs.uUnlit, 0);

            // 1. Draw subtle dark walnut museum base strictly below the biome cube (y < -0.32)
            bindAndDrawMesh(dioramaBaseMesh, mat4FromTRS([0, -0.45, 0], null, [1, 1, 1]), [0.13, 0.09, 0.06, 1.0], null);

            // 2. Draw all meshes from the loaded Blender .glb biome (BiomeSapins.glb, etc.)
            if (state.biomeModel && state.biomeModel.drawCalls) {
                for (const dc of state.biomeModel.drawCalls) {
                    for (const prim of dc.mesh.primitives) {
                        bindAndDrawMesh(prim, dc.worldMatrix, prim.color, null);
                    }
                }
            }

            // 3. Draw the 5 3D Pedestals (Socles 3D) and any exposed 3D Polaroid Cards
            for (const ped of state.pedestals) {
                const px = ped.positionX ?? 0;
                const pz = ped.positionZ ?? 0;
                const py = state.biomeModel
                    ? sampleTerrainY(state.biomeModel, px, pz, ped.positionY ?? 0.30)
                    : (ped.positionY ?? 0.30);
                const isSelected = ped.localSlotIndex === state.selectedSlotIndex;
                const hasCard = !!ped.displayedCard;
                const rTheme = hasCard ? rarityTheme(ped.displayedCard.rarity) : { ring: [0.32, 0.75, 0.56] };

                // Base stone pedestal
                const baseMat = mat4FromTRS([px, py + 0.07, pz], null, isSelected ? [1.12, 1.0, 1.12] : [1.0, 1.0, 1.0]);
                bindAndDrawMesh(
                    pedestalBaseMesh,
                    baseMat,
                    isSelected ? [0.36, 0.42, 0.38, 1.0] : [0.24, 0.28, 0.25, 1.0],
                    null
                );

                // Upper stone platform
                const topMat = mat4FromTRS([px, py + 0.16, pz], null, [1.0, 1.0, 1.0]);
                bindAndDrawMesh(
                    pedestalTopMesh,
                    topMat,
                    hasCard ? [0.32, 0.35, 0.32, 1.0] : [0.20, 0.25, 0.22, 1.0],
                    null
                );

                // Glowing rune ring around pedestal
                gl.uniform1i(locs.uUnlit, 1);
                const pulse = 0.82 + 0.18 * Math.sin(t * 3.2 + ped.localSlotIndex);
                const ringColor = isSelected
                    ? [0.98, 0.84, 0.38, 1.0]
                    : [rTheme.ring[0] * pulse, rTheme.ring[1] * pulse, rTheme.ring[2] * pulse, 1.0];
                const ringMat = mat4FromTRS([px, py + 0.145, pz], null, isSelected ? [1.16, 1.2, 1.16] : [1.0, 1.0, 1.0]);
                bindAndDrawMesh(pedestalRingMesh, ringMat, ringColor, null);

                // 3D Polaroid Card floating above the pedestal
                if (hasCard) {
                    const texKey = `${ped.displayedCard.id}_${ped.displayedCard.imageUrl || ""}`;
                    let tex = state.cardTextures.get(texKey);
                    if (!tex) {
                        tex = buildPolaroidTexture(gl, ped.displayedCard);
                        state.cardTextures.set(texKey, tex);
                    }

                    const bobY = py + 0.72 + Math.sin(t * 2.1 + ped.localSlotIndex * 1.3) * 0.035;
                    // Face toward camera horizontally with gentle sway
                    const angleToCam = Math.atan2(eye[0] - px, eye[2] - pz) + Math.sin(t * 1.2 + ped.localSlotIndex) * 0.08;
                    const halfA = angleToCam * 0.5;
                    const quatY = [0, Math.sin(halfA), 0, Math.cos(halfA)];

                    const cardMat = mat4FromTRS([px, bobY, pz], quatY, isSelected ? [1.08, 1.08, 1.08] : [1.0, 1.0, 1.0]);
                    bindAndDrawMesh(polaroidCardMesh, cardMat, [1, 1, 1, 1], tex);
                }

                gl.uniform1i(locs.uUnlit, 0);
            }

            // 4. Draw floating forest fireflies / golden pollen in 3D space
            gl.uniform1i(locs.uUnlit, 1);
            for (const ff of fireflies) {
                const fx = ff.x + Math.sin(t * ff.speed + ff.phase) * 0.25;
                const fy = ff.y + Math.cos(t * (ff.speed * 1.3) + ff.phase) * 0.14;
                const fz = ff.z + Math.cos(t * ff.speed + ff.phase) * 0.25;
                const fm = mat4FromTRS([fx, fy, fz], null, [1, 1, 1]);
                bindAndDrawMesh(fireflyMesh, fm, [0.95, 0.86, 0.42, 1.0], null);
            }
            gl.uniform1i(locs.uUnlit, 0);

            // 5. Update 2D projected overlay pins for the 5 pedestals
            updateOverlayLabels(state, rect.width, rect.height);

            state.animId = requestAnimationFrame(renderLoop);
        }

        state.animId = requestAnimationFrame(renderLoop);
        return true;
    }

    function updateOverlayLabels(state, cssWidth, cssHeight) {
        if (!state.overlay) return;
        for (const ped of state.pedestals) {
            const pin = state.overlay.querySelector(`[data-pedestal-slot="${ped.localSlotIndex}"]`);
            if (!pin) continue;

            const anchorY = ped.displayedCard ? (ped.positionY + 1.18) : (ped.positionY + 0.36);
            const proj = projectPointToScreen([ped.positionX, anchorY, ped.positionZ], state.lastViewProj, cssWidth, cssHeight);
            if (!proj || proj.x < -40 || proj.x > cssWidth + 40 || proj.y < -40 || proj.y > cssHeight + 40) {
                pin.style.opacity = "0";
                pin.style.pointerEvents = "none";
            } else {
                pin.style.opacity = "1";
                pin.style.pointerEvents = "auto";
                pin.style.transform = `translate3d(${proj.x.toFixed(1)}px, ${proj.y.toFixed(1)}px, 0) translate(-50%, -100%)`;
            }
        }
    }

    function handleCanvasClick(state, event) {
        const rect = state.canvas.getBoundingClientRect();
        const clickX = event.clientX - rect.left;
        const clickY = event.clientY - rect.top;

        // 1. Check if user clicked on a 3D pedestal or 3D card
        let closestSlot = -1;
        let closestDist = 44; // pixels radius
        for (const ped of state.pedestals) {
            const terrainY = state.biomeModel
                ? sampleTerrainY(state.biomeModel, ped.positionX, ped.positionZ, ped.positionY)
                : ped.positionY;
            const centerY = ped.displayedCard ? (terrainY + 0.55) : (terrainY + 0.15);
            const proj = projectPointToScreen([ped.positionX, centerY, ped.positionZ], state.lastViewProj, rect.width, rect.height);
            if (proj) {
                const d = Math.hypot(clickX - proj.x, clickY - proj.y);
                if (d < closestDist) {
                    closestDist = d;
                    closestSlot = ped.localSlotIndex;
                }
            }
        }

        if (closestSlot >= 0 && !state.placementMode) {
            state.selectedSlotIndex = closestSlot;
            if (state.dotNetRef) {
                state.dotNetRef.invokeMethodAsync("OnPedestalSelectedFrom3D", closestSlot);
            }
            return;
        }

        // 2. If placementMode is active, raycast directly onto the 3D biome ground triangles (dunes, slopes, flat ground)
        if (state.placementMode) {
            const invVP = mat4Invert(state.lastViewProj);
            if (!invVP) return;

            const ndcX = (clickX / rect.width) * 2.0 - 1.0;
            const ndcY = 1.0 - (clickY / rect.height) * 2.0;

            const nearPt = unproject(ndcX, ndcY, -1.0, invVP);
            const farPt = unproject(ndcX, ndcY, 1.0, invVP);

            let hitX = null, hitY = null, hitZ = null;
            const meshHit = raycastBiomeGround(state.biomeModel, nearPt, farPt);
            if (meshHit) {
                hitX = Math.max(-2.9, Math.min(2.9, meshHit[0]));
                hitY = meshHit[1];
                hitZ = Math.max(-2.9, Math.min(2.9, meshHit[2]));
            } else {
                const dirY = farPt[1] - nearPt[1];
                if (Math.abs(dirY) > 1e-5) {
                    const planeY = 0.30;
                    const t = (planeY - nearPt[1]) / dirY;
                    if (t > 0) {
                        hitX = Math.max(-2.9, Math.min(2.9, nearPt[0] + (farPt[0] - nearPt[0]) * t));
                        hitY = planeY;
                        hitZ = Math.max(-2.9, Math.min(2.9, nearPt[2] + (farPt[2] - nearPt[2]) * t));
                    }
                }
            }

            if (hitX !== null) {
                const selPed = state.pedestals.find(p => p.localSlotIndex === state.selectedSlotIndex);
                if (selPed) {
                    selPed.positionX = hitX;
                    selPed.positionY = hitY;
                    selPed.positionZ = hitZ;
                }

                if (state.dotNetRef) {
                    state.dotNetRef.invokeMethodAsync(
                        "OnTerrainClickedFrom3D",
                        Math.round(hitX * 100) / 100,
                        Math.round(hitY * 100) / 100,
                        Math.round(hitZ * 100) / 100
                    );
                }
            }
        }
    }

    async function updateScene(canvasId, sceneData) {
        const state = instances.get(canvasId);
        if (!state || !sceneData) return;

        state.pedestals = sceneData.pedestals || [];
        if (typeof sceneData.selectedSlotIndex === "number") {
            state.selectedSlotIndex = sceneData.selectedSlotIndex;
        }
        if (typeof sceneData.placementMode === "boolean") {
            state.placementMode = sceneData.placementMode;
        }

        if (sceneData.glbUrl && sceneData.glbUrl !== state.glbUrl) {
            state.glbUrl = sceneData.glbUrl;
            try {
                state.biomeModel = await loadGlbModel(state.gl, sceneData.glbUrl);
                if (state.biomeModel && typeof state.biomeModel.targetCenterY === "number") {
                    state.camera.goalTarget = [0.0, state.biomeModel.targetCenterY, 0.1];
                }
            } catch (err) {
                console.error("[BiomaVitrine3D] Erreur chargement GLB:", err);
                state.biomeModel = null;
            }
        } else if (!sceneData.glbUrl) {
            state.glbUrl = null;
            state.biomeModel = null;
        }
    }

    function setCameraView(canvasId, preset, slotIndex) {
        const state = instances.get(canvasId);
        if (!state) return;

        if (preset === "reset") {
            state.camera.goalTheta = 0.62;
            state.camera.goalPhi = 0.98;
            state.camera.goalRadius = 7.4;
            state.camera.goalTarget = [0.0, state.biomeModel?.targetCenterY ?? 0.45, 0.1];
        } else if (preset === "top") {
            state.camera.goalTheta = 0.0;
            state.camera.goalPhi = 0.32;
            state.camera.goalRadius = 7.4;
            state.camera.goalTarget = [0.0, 0.30, 0.0];
        } else if (preset === "focus" && typeof slotIndex === "number") {
            const ped = state.pedestals.find(p => p.localSlotIndex === slotIndex);
            if (ped) {
                state.selectedSlotIndex = slotIndex;
                state.camera.goalTarget = [ped.positionX, ped.positionY + 0.45, ped.positionZ];
                state.camera.goalRadius = 3.4;
                state.camera.goalPhi = 1.08;
            }
        }
    }

    function setOptions(canvasId, opts) {
        const state = instances.get(canvasId);
        if (!state || !opts) return;
        if (typeof opts.autoRotate === "boolean") state.autoRotate = opts.autoRotate;
        if (typeof opts.placementMode === "boolean") state.placementMode = opts.placementMode;
        if (typeof opts.atmosphere === "string") state.atmosphere = opts.atmosphere;
        if (typeof opts.selectedSlotIndex === "number") state.selectedSlotIndex = opts.selectedSlotIndex;
    }

    function dispose(canvasId) {
        const state = instances.get(canvasId);
        if (!state) return;
        state.disposed = true;
        if (state.animId) cancelAnimationFrame(state.animId);
        instances.delete(canvasId);
    }

    return {
        init,
        updateScene,
        setCameraView,
        setOptions,
        dispose
    };
})();
