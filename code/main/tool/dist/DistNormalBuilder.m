classdef DistNormalBuilder < DistBuilder
    %DISTNORMALBUILDER DistBuilder for DistNormal
    %    Work for 1d DistNormal currently. Support DistArray.
    
    properties
    end
    
    methods
      
        function L=empty(this, r, c)
            L = DistNormal.empty(r, c);
        end
        
        function S=getStat(this, D)
            assert(isa(D, 'DistNormal') || isa(D, 'DistArray'));
            assert(~isa(D, 'DistArray') || isa(D.distArray, 'DistNormal'));
            assert(D(1).d==1, 'Only 1d Gaussian is supported');
            M = [D.mean];
            M2 = [D.variance] + M.^2;
            S = [M; M2];
        end
        
        function D=fromStat(this, S)
            % Construct DistNormal(s) from a sufficient statistics returned
            % from getStat()
            assert(size(S,1)==2, 'Only univariate Gaussian is supported');
            M = S(1,:);
            V = S(2,:)-M.^2;
            D = DistNormal(M, V);
        end
        
        function D= fromSamples(this, samples, weights)
            assert(size(samples, 1)==1, 'Only 1d Gaussian is supported');
            S = this.suffStat(samples);
            SW = S*weights(:);
            m = SW(1);
            v = SW(2)-m^2;
            D = DistNormal(m ,v);
        end
        
        function s = shortSummary(this)
            s = mfilename;
        end
        %%%%%%%%%%%%%%%%%%%%5
        
        function S=suffStat(this, X)
            % phi(x)=[x, x^2]' or phi(x)=[x; vec(xx')]
            % X (dxn)
            [d,n] = size(X);
            assert(d>=1)
            if d==1
                S = [X; X.^2];
            else
                S = zeros(d+d^2, n);
                % very slow. Improve later
                for i=1:n
                    Xi = X(:, i);
                    S(:, i) = [Xi; reshape(Xi*Xi', d^2, 1)];
                end
            end
            
        end 
%         

%         function T=stableSuffStat(this, S)
%             assert(size(S,1)==2, 'Only univariate Gaussian is supported');
%             M = S(1,:);
%             V = S(2,:)-M.^2;
%             Mok = isfinite(M);
%             Vok = isfinite(V) & abs(V)>1e-4 ;
%             T = Mok & Vok;
%         end
%         
%         function obj=dummyObj(this)
%             obj = DistNormal(nan, nan);
%         end
    end
    
end

