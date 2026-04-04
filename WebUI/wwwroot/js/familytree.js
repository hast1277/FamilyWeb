window.familyTree = (() => {
    'use strict';

    let cy = null;
    let dotNetRef = null;

    function getPageGrid(scaleFactor = 1.0, orientation = 'landscape') {
        if (!cy) {
            return { rows: 0, cols: 0, pages: 0 };
        }

        const margin = 10; // mm
        const isPortrait = orientation === 'portrait';
        const a4Width = isPortrait ? 210 : 297;
        const a4Height = isPortrait ? 297 : 210;
        const contentWidth = a4Width - margin * 2;
        const contentHeight = a4Height - margin * 2;

        const bb = cy.elements().boundingBox();
        const imgWidth = Math.max(1, bb.w * scaleFactor);
        const imgHeight = Math.max(1, bb.h * scaleFactor);

        const fit = Math.min(contentWidth / imgWidth, contentHeight / imgHeight);
        if (fit >= 1) {
            return { rows: 1, cols: 1, pages: 1 };
        }

        const cols = Math.ceil(imgWidth / contentWidth);
        const rows = Math.ceil(imgHeight / contentHeight);
        return { rows, cols, pages: rows * cols };
    }

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
                    label: n.type === 'person'
                        ? ((n.label ?? '')
                            + (n.birthday ? '\n* ' + n.birthday : '')
                            + (n.deathDate ? '\n† ' + n.deathDate : ''))
                        : '',
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
                    edgeType: e.type,
                    label: e.label ?? ''
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
                        'height': 98,
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
                        'arrow-scale': 1.2,
                        'label': 'data(label)',
                        'font-size': 9,
                        'color': '#0f172a',
                        'text-background-color': '#ffffff',
                        'text-background-opacity': 0.9,
                        'text-background-padding': 2,
                        'text-margin-y': -6
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

    function exportToPdf(scaleFactor = 1.0, orientation = 'landscape') {
        if (!cy) return;

        const { jsPDF } = window.jspdf;
        const margin = 10; // mm
        const isPortrait = orientation === 'portrait';
        const a4Width = isPortrait ? 210 : 297;
        const a4Height = isPortrait ? 297 : 210;
        const contentWidth = a4Width - margin * 2;
        const contentHeight = a4Height - margin * 2;

        // Get the bounding box of the tree
        const bb = cy.elements().boundingBox();

        // Render the tree as PNG with higher resolution for better quality when scaled
        const renderScale = 3 * scaleFactor;
        const png64 = cy.png({ output: 'base64', full: true, scale: renderScale, bg: '#ffffff' });

        // Calculate image dimensions in mm
        const imgWidth = bb.w * scaleFactor;
        const imgHeight = bb.h * scaleFactor;

        // Determine if we need multiple pages
        const fit = Math.min(contentWidth / imgWidth, contentHeight / imgHeight);
        const needsMultiplePages = fit < 1;

        // Create image element to get dimensions for cropping
        const img = new Image();
        img.onload = function() {
            const pixelWidth = img.width;
            const pixelHeight = img.height;

            const pdf = new jsPDF({
                orientation,
                unit: 'mm',
                format: 'a4',
                compress: true
            });

            if (!needsMultiplePages) {
                // Single page: center the image
                const x = margin + (contentWidth - imgWidth) / 2;
                const y = margin + (contentHeight - imgHeight) / 2;
                pdf.addImage('data:image/png;base64,' + png64, 'PNG', x, y, imgWidth, imgHeight);
                pdf.save('familjtrad.pdf');
            } else {
                // Multiple pages: create crops and add to PDF
                const pixelsPerMm = Math.max(pixelWidth / imgWidth, pixelHeight / imgHeight);
                const contentPixelWidth = contentWidth * pixelsPerMm;
                const contentPixelHeight = contentHeight * pixelsPerMm;

                const cols = Math.ceil(pixelWidth / contentPixelWidth);
                const rows = Math.ceil(pixelHeight / contentPixelHeight);

                let pageCount = 0;
                for (let row = 0; row < rows; row++) {
                    for (let col = 0; col < cols; col++) {
                        if (pageCount > 0) {
                            pdf.addPage('a4', orientation);
                        }

                        // Create canvas for this page's content
                        const canvas = document.createElement('canvas');
                        canvas.width = Math.min(contentPixelWidth, pixelWidth - col * contentPixelWidth);
                        canvas.height = Math.min(contentPixelHeight, pixelHeight - row * contentPixelHeight);
                        const ctx = canvas.getContext('2d');

                        // Draw the relevant portion of the image
                        ctx.drawImage(
                            img,
                            col * contentPixelWidth, row * contentPixelHeight,
                            canvas.width, canvas.height,
                            0, 0,
                            canvas.width, canvas.height
                        );

                        // Add canvas image to PDF
                        const canvasDataUrl = canvas.toDataURL('image/png');
                        const displayWidth = canvas.width / pixelsPerMm;
                        const displayHeight = canvas.height / pixelsPerMm;
                        pdf.addImage(canvasDataUrl, 'PNG', margin, margin, displayWidth, displayHeight);

                        pageCount++;
                    }
                }

                pdf.save('familjtrad.pdf');
            }
        };
        img.src = 'data:image/png;base64,' + png64;
    }

    function getEstimatedPageCount(scaleFactor = 1.0, orientation = 'landscape') {
        const { pages } = getPageGrid(scaleFactor, orientation);
        return pages;
    }

    function destroy() {
        if (cy) {
            cy.destroy();
            cy = null;
        }
        dotNetRef = null;
    }

    return { init, exportToPdf, getEstimatedPageCount, destroy };
})();
