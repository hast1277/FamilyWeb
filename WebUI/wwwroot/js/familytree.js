window.familyTree = (() => {
    'use strict';

    let cy = null;
    let dotNetRef = null;

    function init(dotNetReference, containerId, nodes, edges) {
        dotNetRef = dotNetReference;

        if (cy) {
            cy.destroy();
            cy = null;
        }

        const container = document.getElementById(containerId);
        if (!container) return;

        const elements = [
            ...nodes.map(n => ({
                data: {
                    id: n.id,
                    nodeType: n.type,
                    label: n.type === 'person' ? (n.label ?? '') : '',
                    personId: n.personId,
                    photo: n.photo ? `/img/Family/${n.photo}` : ''
                },
                position: { x: n.x ?? 0, y: n.y ?? 0 }
            })),
            ...edges.map(e => ({
                data: {
                    id: `e-${e.fromId}-${e.toId}`,
                    source: e.fromId,
                    target: e.toId,
                    edgeType: e.type
                }
            }))
        ];

        cy = cytoscape({
            container,
            elements,
            layout: { name: 'preset' },
            style: [
                {
                    selector: 'node[nodeType="person"]',
                    style: {
                        'shape': 'round-rectangle',
                        'width': 180,
                        'height': 70,
                        'background-color': '#ffffff',
                        'background-image': ele => ele.data('photo') || 'none',
                        'background-fit': 'none',
                        'background-width': 52,
                        'background-height': 62,
                        'background-position-x': 7,
                        'background-position-y': '50%',
                        'label': 'data(label)',
                        'font-size': 11,
                        'text-wrap': 'wrap',
                        'text-max-width': 108,
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'text-margin-x': 28,
                        'color': '#1e3a5f',
                        'font-weight': 'bold',
                        'border-width': 1.5,
                        'border-color': '#3b82f6',
                        'cursor': 'pointer'
                    }
                },
                {
                    selector: 'node[nodeType="person"][!photo]',
                    style: {
                        'background-color': '#dbeafe',
                        'text-margin-x': 0,
                        'text-max-width': 160
                    }
                },
                {
                    selector: 'node[nodeType="person"]:selected',
                    style: {
                        'border-color': '#f59e0b',
                        'border-width': 3,
                        'text-background-color': '#fef3c7'
                    }
                },
                {
                    selector: 'node[nodeType="union"]',
                    style: {
                        'shape': 'ellipse',
                        'width': 14,
                        'height': 14,
                        'background-color': '#3b82f6',
                        'border-width': 1.5,
                        'border-color': '#1d4ed8',
                        'label': ''
                    }
                },
                {
                    selector: 'edge[edgeType="spouse"]',
                    style: {
                        'width': 2,
                        'line-color': '#94a3b8',
                        'curve-style': 'straight',
                        'target-arrow-shape': 'none'
                    }
                },
                {
                    selector: 'edge[edgeType="child"]',
                    style: {
                        'width': 1.5,
                        'line-color': '#64748b',
                        'curve-style': 'straight',
                        'target-arrow-shape': 'triangle',
                        'target-arrow-color': '#64748b',
                        'arrow-scale': 1.2
                    }
                }
            ],
            userZoomingEnabled: true,
            userPanningEnabled: true,
            boxSelectionEnabled: false,
            minZoom: 0.1,
            maxZoom: 3
        });

        cy.on('tap', 'node[nodeType="person"]', evt => {
            const personId = evt.target.data('personId');
            if (personId != null && dotNetRef) {
                dotNetRef.invokeMethodAsync('OnPersonClicked', personId);
            }
        });

        cy.fit(cy.elements(), 40);
    }

    function exportToPdf() {
        if (!cy) return;

        const { jsPDF } = window.jspdf;
        const scale = 2;
        const padding = 40;
        const bb = cy.elements().boundingBox();
        const imgW = Math.ceil((bb.w + padding * 2) * scale);
        const imgH = Math.ceil((bb.h + padding * 2) * scale);

        const png64 = cy.png({ output: 'base64', full: true, scale, bg: '#ffffff' });

        const orientation = imgW >= imgH ? 'landscape' : 'portrait';
        const pdf = new jsPDF({ orientation, unit: 'px', format: [imgW, imgH], compress: true });
        pdf.addImage('data:image/png;base64,' + png64, 'PNG', 0, 0, imgW, imgH);
        pdf.save('familjtrad.pdf');
    }

    function destroy() {
        if (cy) {
            cy.destroy();
            cy = null;
        }
        dotNetRef = null;
    }

    return { init, exportToPdf, destroy };
})();
