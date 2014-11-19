classdef KLaplaceFC < MPFunctionClass
    %KLAPLACEFC A function class defining Laplace kernels on parameters of %incoming messages.
    %   .
    
    properties(SetAccess = protected)
        % input samples. Instances. n samples.
        % Inherited
        %inputInstances;

        featureExtractor;

        % features extracted from the samples. 
        % This can be big as samples may be big.
        % #features x n
        inputFeatures;

        % widths matrix. Each column is to be used with one Laplace kernel.
        %widthsMat
        widths;

        % Laplace center samples.
        %centerInstances;
        % #features x #total basis (number of total basis functions)
        centerFeatures;

        % indices (not 0-1) indicating which centers are marked to be included 
        % in the final function.
        markedInd;

        % W matrix where each column is one w_i of length dim(output)
        weightMat = [];
    end
    
    methods
        function this = KLaplaceFC(widths, fe, centerInstances, inputInstances)
            % widths = parameters as in KLaplace
            % fe = a FeatureExtractor
            % laplaceCenters = instances to be used as centers for the kernel
            assert(isnumeric(widths));
            assert(all(widths > 0));
            assert(isa(fe, 'FeatureExtractor'));
            assert(isa(inputInstances, 'Instances'));
            assert(isa(centerInstances, 'Instances'));

            this.widths = widths;
            this.inputInstances = inputInstances;
            %this.centerInstances = centerInstances;
            % cached features on all samples.
            %
            % Users have to make sure that the specified FeatureExtractor is 
            % compatible with the samples.
            this.inputFeatures = fe.extractFeatures(inputInstances);
            this.featureExtractor = fe;
            this.centerFeatures = fe.extractFeatures(centerInstances);
            this.markedInd = [];
        end

        function [crossRes, G, wt, searchMemento] = findBestBasisFunction(this, R, regParam)
            C = this.centerFeatures;
            
            if isempty(C) || length(this.markedInd) == size(this.centerFeatures, 2)
                % If all candidates are selected,...
                % no more candidates 
                crossRes = -inf(1);
                G = [];
                searchMemento = [];
                return;
            end
            n = size(this.inputFeatures, 2);
            % should externalize this as an option
            % Evaluate on subset of training samples to reduce computation
            subsample = 500;
            warning('externalize subsample option')
            If = randperm(n, min(subsample, n));
            totalBasis = size(this.centerFeatures, 2);
            I = setdiff(1:totalBasis, this.markedInd);
            Kmat = this.evaluateOnTrainingCenterInd(I, If);

            % row vector. same length as the #center instances
            z = sum(Kmat.*Kmat, 2)' + regParam;
            % cross correlations on subset of inputInstances
            Cross = sum( (R(:, If)*Kmat').^2, 1) ./ z;
            % pick best basis function g by taking max cross correlation
            [crossRes, mi] = max(Cross);
            gind = I(mi);
            % Evaluate the function on full data set 
            G = this.evaluateOnTrainingCenterInd(gind, 1:n);
            % compute w
            wt = R*G'/(G*G' + regParam);
            % search memento for marking the selected function later 
            mem = struct();
            mem.centerIndex = gind;
            mem.wt = wt;
            searchMemento = mem;
        end

        function markBestBasisFunction(this, searchMemento)
            assert(isstruct(searchMemento));
            cind = searchMemento.centerIndex;
            assert(~ismember(cind, this.markedInd));
            % This will affect next findBestBasisFunction() and evaluate()
            this.markedInd = [this.markedInd, cind];
            wt = searchMemento.wt;
            this.weightMat = [this.weightMat, wt];
        end

        function Func = evalFunction(this, X)
            assert(isa(X, 'Instances'));
            lwidths = this.widths;
            F = this.featureExtractor.extractFeatures(X);
            C = this.centerFeatures;
            Fsc = diag(1./lwidths)*F;
            Csc = diag(1./lwidths)*C(:, this.markedInd);
            % C x F
            D = sqrt(bsxfun(@plus, sum(Csc.^2, 1)', sum(Fsc.^2, 1)) - 2*Csc'*Fsc);
            Kmat = exp(-D);
            W = this.weightMat;
            Func = W*Kmat;
        end

        function G = evaluate(this, X)
            % return #marked x sample size 
            lwidths = this.widths;
            Fx = this.featureExtractor.extractFeatures(X);
            C = this.centerFeatures;
            Fsc = diag(1./lwidths)*Fx;
            Csc = diag(1./lwidths)*C(:, this.markedInd);
            % C x F
            D = sqrt(bsxfun(@plus, sum(Csc.^2, 1)', sum(Fsc.^2, 1)) - 2*Csc'*Fsc);
            Kmat = exp(-D);
            G = Kmat;
        end

        function G = evaluateOnTraining(this)
            % return: G (#marked x inputInstances count)
            n = length(this.inputInstances);
            G = this.evaluateOnTrainingCenterInd(this.markedInd, 1:n);

        end

        function G = evaluateOnTrainingSubset(this, Ind)
            % return: G (#marked x length(Ind ) )
            G = this.evaluateOnTrainingCenterInd(this.markedInd, Ind);

        end

        function G = evaluateOnTrainingCenterInd(this, centerInd, sampleInd)
            lwidths = this.widths;
            F = this.inputFeatures;
            C = this.centerFeatures;
            Fsc = diag(1./lwidths)*F;
            FscSub = Fsc(:, sampleInd);
            Csc = diag(1./lwidths)*C(:, centerInd);
            % C x F
            D = sqrt(bsxfun(@plus, sum(Csc.^2, 1)', sum(FscSub.^2, 1)) - 2*Csc'*FscSub);
            Kmat = exp(-D);
            assert(all(size(Kmat) == [length(centerInd), length(sampleInd)]));
            G = Kmat;
        end

        function obj = finalize(this)
            % construct a dummy obj. Modify later.
            obj = KLaplaceFC(this.widths, this.featureExtractor, ...
                this.centerInstances.instances(1), this.inputInstances.instances(1));
            obj.inputInstances = [];
            obj.inputFeatures = [];
            obj.centerFeatures = this.centerFeatures(:, this.markedInd);
            obj.markedInd = 1:size(obj.centerFeatures, 2);
        end

        function setWeightMatrix(this, W)
            assert(length(this.markedInd) == size(W, 2));
            this.weightMat = W;
        end

        function c = countSelectedBases(this)
            c = length(this.markedInd);
            assert(length(this.markedInd) == length(unique(this.markedInd)));
        end
    end
    
    methods(Access=private)
    end
    
end
